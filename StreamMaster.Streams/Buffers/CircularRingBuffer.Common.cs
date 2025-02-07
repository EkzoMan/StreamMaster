﻿using Microsoft.Extensions.Logging;

using Prometheus;

namespace StreamMaster.Infrastructure.VideoStreamManager.Buffers;


/// <summary>
/// Represents a circular ring buffer for streaming data.
/// </summary>
public sealed partial class CircularRingBuffer : ICircularRingBuffer
{

    private static readonly Gauge _waitTime = Metrics.CreateGauge(
  "sm_circular_buffer_read_wait_for_data_availability_duration_milliseconds",
        "Client waiting duration in milliseconds for data availability",
new GaugeConfiguration
{
    LabelNames = ["circular_buffer_id", "client_id", "video_stream_name"]

});

    private static readonly Gauge _bitsPerSecond = Metrics.CreateGauge(
"sm_circular_buffer_read_stream_bits_per_second",
"Bits per second read from the input stream.",
new GaugeConfiguration
{
    LabelNames = ["circular_buffer_id", "video_stream_name"]
});

    private static readonly Counter _bytesWrittenCounter = Metrics.CreateCounter(
        "sm_circular_buffer_bytes_written_total",
        "Total number of bytes written.",
        new CounterConfiguration
        {
            LabelNames = ["circular_buffer_id", "video_stream_name"]
        });

    private static readonly Counter _writeErrorsCounter = Metrics.CreateCounter(
        "sm_circular_buffer_write_errors_total",
        "Total number of write errors.",
         new CounterConfiguration
         {
             LabelNames = ["circular_buffer_id", "video_stream_name"]
         });

    private static readonly Gauge _dataArrival = Metrics.CreateGauge(
"sm_circular_buffer_arrival_time_milliseconds",
    "Data arrival times in milliseconds.",
new GaugeConfiguration
{
    LabelNames = ["circular_buffer_id", "video_stream_name"]
});
    private readonly object bufferLock = new();

    public Memory<byte> GetBufferSlice(int length)
    {
        int bufferEnd = _writeIndex + length;

        if (bufferEnd <= _bufferSize)
        {
            // No wrap-around needed
            return _buffer.Slice(_writeIndex, length);
        }
        else
        {
            // Handle wrap-around
            int lengthToEnd = _bufferSize - _writeIndex;
            int lengthFromStart = length - lengthToEnd;

            // Create a temporary array to hold the wrapped data
            byte[] result = new byte[length];

            // Copy from _oldestDataIndex to the end of the buffer
            _buffer.Slice(_writeIndex, lengthToEnd).CopyTo(result);

            // Copy from start of the buffer to fill the remaining length
            _buffer[..lengthFromStart].CopyTo(result.AsMemory(lengthToEnd));

            return result;
        }
    }


    public List<StreamStatisticsResult> GetAllStatisticsForAllUrls()
    {
        List<StreamStatisticsResult> allStatistics = [];

        IInputStreamingStatistics input = GetInputStreamStatistics();

        foreach (ClientStreamingStatistics stat in _statisticsManager.GetAllClientStatisticsByClientIds(_clientReadIndexes.Keys))
        {
            allStatistics.Add(new StreamStatisticsResult
            {
                Id = Guid.NewGuid().ToString(),
                CircularBufferId = Id.ToString(),
                ChannelId = StreamInfo.ChannelId,
                ChannelName = StreamInfo.ChannelName,
                VideoStreamId = StreamInfo.VideoStreamId,
                VideoStreamName = StreamInfo.VideoStreamName,
                M3UStreamingProxyType = StreamInfo.StreamingProxyType,
                Logo = StreamInfo.Logo,
                Rank = StreamInfo.Rank,

                InputBytesRead = input.BytesRead,
                InputBytesWritten = input.BytesWritten,
                InputBitsPerSecond = input.BitsPerSecond,
                InputStartTime = input.StartTime,

                StreamUrl = StreamInfo.StreamUrl,

                ClientBitsPerSecond = stat.ReadBitsPerSecond,
                ClientBytesRead = stat.BytesRead,
                ClientId = stat.ClientId,
                ClientStartTime = stat.StartTime,
                ClientAgent = stat.ClientAgent,
                ClientIPAddress = stat.ClientIPAddress
            });
        }

        return allStatistics;
    }

    public void ResizeBuffer(int newSize)
    {
        lock (bufferLock) // Ensure exclusive access
        {
            Memory<byte> newBuffer = new byte[newSize];
            _buffer.CopyTo(newBuffer);
            _buffer = newBuffer;
        }
    }

    private int GetOldestReadIndex()
    {
        return _clientReadIndexes.Values.DefaultIfEmpty(0).Min();
    }

    public void RegisterClient(IClientStreamerConfiguration streamerConfiguration)
    {
        int index = 0;
        if (HasBufferFlipped)
        {
            int bufferLength = _buffer.Length;

            //int increaseBy = (int)(currentSize * 0.20); // 20% increase
            //int maxSize = _originalBufferSize * 4; // Max

            index = (_writeIndex + (int)(bufferLength * 0.10)) % bufferLength;
        }

        if (_clientReadIndexes.TryAdd(streamerConfiguration.ClientId, index))
        {
            _statisticsManager.RegisterClient(streamerConfiguration);
            _logger.LogInformation("Registered new client {ClientId} with read index {ReadIndex}", streamerConfiguration.ClientId, index);
        }
        else
        {
            _logger.LogWarning("Failed to add new client {ClientId} to read indexes.", streamerConfiguration.ClientId);
        }
    }

    private void ResizeBuffer()
    {
        int currentSize = _buffer.Length;

        int increaseBy = (int)(currentSize * 0.20); // 20% increase
        int maxSize = _originalBufferSize * 4; // Maximum size, e.g., 4 times the original size

        // Calculate the new size, ensuring it's not greater than the max
        int newSize = Math.Min(currentSize + increaseBy, maxSize);

        // Check if new size is actually larger than current size
        if (newSize > currentSize)
        {
            _logger.LogInformation($"Resizing buffer from {currentSize} to {newSize}");
            try
            {
                ResizeBuffer(newSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while resizing the buffer");
                // Handle or rethrow the exception as needed
            }
        }
        else
        {
            //_logger.LogInformation("Buffer resize not required or maximum size reached.");
        }
    }

    public void UnRegisterClient(Guid clientId)
    {
        _ = _clientReadIndexes.TryRemove(clientId, out _);
        _statisticsManager.UnRegisterClient(clientId);

        _logger.LogInformation("UnRegisterClient for clientId: {clientId}  {VideoStreamName}", clientId, StreamInfo.VideoStreamName);
    }

    public ICollection<Guid> GetClientIds()
    {
        return _clientReadIndexes.Keys;
    }

    private IInputStreamingStatistics GetInputStreamStatistics()
    {
        return _inputStreamStatistics;
    }

    public int GetReadIndex(Guid clientId)
    {
        return _clientReadIndexes[clientId];
    }

    private void DoDispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_bufferHealthLogger != null)
                {
                    _bufferHealthLogger.Change(Timeout.Infinite, Timeout.Infinite); // Stop the timer
                    _bufferHealthLogger.Dispose(); // Dispose the timer
                }
                _writeSignal.TrySetCanceled();
                _waitTime.GetAllLabelValues().ToList().ForEach(x => _waitTime.RemoveLabelled(x[0], x[1], x[2]));
                _bitsPerSecond.RemoveLabelled(Id.ToString(), StreamInfo.VideoStreamName);
                _bytesWrittenCounter.RemoveLabelled(Id.ToString(), StreamInfo.VideoStreamName);
                _writeErrorsCounter.RemoveLabelled(Id.ToString(), StreamInfo.VideoStreamName);
                _dataArrival.RemoveLabelled(Id.ToString(), StreamInfo.VideoStreamName);
                foreach (KeyValuePair<Guid, int> item in _clientReadIndexes)
                {
                    _statisticsManager.UnRegisterClient(item.Key);
                }
                _clientReadIndexes.Clear();
                _clientLastReadBeforeOverwrite.Clear();
                _performanceMetrics.Clear();
            }

            // Dispose unmanaged resources here if any

            _disposed = true;
        }


    }

    // Public implementation of Dispose pattern callable by consumers
    public void Dispose()
    {
        DoDispose(true);
        GC.SuppressFinalize(this);
    }

    // Finalizer in case Dispose wasn't called
    ~CircularRingBuffer()
    {
        DoDispose(false);
    }

}
