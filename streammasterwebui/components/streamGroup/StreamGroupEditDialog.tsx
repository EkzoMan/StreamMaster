import { type StreamGroupDto, type UpdateStreamGroupRequest } from '@lib/iptvApi';
import { InputText } from 'primereact/inputtext';
import { memo, useCallback, useEffect, useMemo, useState } from 'react';

import { useSelectedStreamGroup } from '@lib/redux/slices/useSelectedStreamGroup';

import StreamGroupChannelGroupsSelector from '@features/streamGroupEditor/StreamGroupChannelGroupsSelector';
import { UpdateStreamGroup } from '@lib/smAPI/StreamGroups/StreamGroupsMutateAPI';
import { v4 as uuidv4 } from 'uuid';
import InfoMessageOverLayDialog from '../InfoMessageOverLayDialog';
import EditButton from '../buttons/EditButton';

interface StreamGroupEditDialogProperties {
  readonly id: string;
  readonly onHide?: (value: StreamGroupDto | undefined) => void;
}

const StreamGroupEditDialog = (props: StreamGroupEditDialogProperties) => {
  const [showOverlay, setShowOverlay] = useState<boolean>(false);
  const [block, setBlock] = useState<boolean>(false);
  const [infoMessage, setInfoMessage] = useState('');
  const [name, setName] = useState<string>('');
  // const [autoSetChannelNumbers, setAutoSetChannelNumbers] = useState<boolean>(false);
  const uuid = uuidv4();
  const { selectedStreamGroup } = useSelectedStreamGroup(props.id);

  useEffect(() => {
    if (selectedStreamGroup === undefined) {
      return;
    }

    if (selectedStreamGroup.name !== undefined) {
      setName(selectedStreamGroup.name);
    }
  }, [selectedStreamGroup]);

  const ReturnToParent = useCallback(
    (returnValueData?: StreamGroupDto) => {
      setShowOverlay(false);
      setInfoMessage('');
      setBlock(false);
      props.onHide?.(returnValueData);
    },
    [props]
  );

  const isSaveEnabled = useMemo((): boolean => {
    if (selectedStreamGroup === undefined || selectedStreamGroup.id === undefined) {
      return false;
    }

    if (name && name !== '' && selectedStreamGroup.name !== name) {
      return true;
    }

    if (name && name !== '') {
      return true;
    }

    return false;
  }, [name, selectedStreamGroup]);

  const onUpdate = useCallback(() => {
    setBlock(true);

    if (!isSaveEnabled) {
      ReturnToParent();

      return;
    }

    const data = {} as UpdateStreamGroupRequest;
    data.name = name;
    data.streamGroupId = selectedStreamGroup.id;

    UpdateStreamGroup(data)
      .then(() => {
        setInfoMessage('Stream Group Edit Successfully');
      })
      .catch((error) => {
        setInfoMessage(`Stream Group Edit Error: ${error.message}`);
      });
  }, [ReturnToParent, isSaveEnabled, name, selectedStreamGroup]);

  useEffect(() => {
    const callback = (event: KeyboardEvent) => {
      if (event.code === 'Enter' || event.code === 'NumpadEnter') {
        event.preventDefault();

        if (name !== '') {
          onUpdate();
        }
      }
    };

    document.addEventListener('keydown', callback);

    return () => {
      document.removeEventListener('keydown', callback);
    };
  }, [onUpdate, name]);

  return (
    <>
      <InfoMessageOverLayDialog
        blocked={block}
        closable
        header="Edit Stream Group"
        infoMessage={infoMessage}
        onClose={() => {
          ReturnToParent();
        }}
        overlayColSize={4}
        show={showOverlay}
      >
        <div className="flex grid justify-content-between align-items-center">
          <div className="flex col-12">
            <label className="col-2 " htmlFor="Name">
              Name:{' '}
            </label>
            <div className="col-8 ">
              <InputText autoFocus className="bordered-text-large" id={uuid} onChange={(e) => setName(e.target.value)} type="text" value={name} />
            </div>
          </div>
          <div className="flex col-12 ">
            <label className="col-2 ">Groups: </label>
            <div className="col-8 ">
              <StreamGroupChannelGroupsSelector streamGroupId={selectedStreamGroup?.id ?? undefined} />
            </div>
          </div>
          {/* <div className="flex col-12 ">
            <label className="col-6">Auto Set Channel Numbers: </label>
            <div className="col-2">
              <Checkbox
                checked={autoSetChannelNumbers}
                onChange={async (e: CheckboxChangeEvent) => {
                  setAutoSetChannelNumbers(e.checked ?? false);
                }}
                tooltip="Enable Auto Channel Numbering"
                tooltipOptions={getTopToolOptions}
              />
            </div>
          </div> */}

          <div className="flex col-12 mt-3 gap-2 justify-content-end">
            <EditButton disabled={!isSaveEnabled} label="Edit Stream Group" onClick={() => onUpdate()} tooltip="Edit Stream Group" />
          </div>
        </div>
      </InfoMessageOverLayDialog>

      <EditButton
        disabled={selectedStreamGroup === undefined || selectedStreamGroup.id === undefined || selectedStreamGroup.id < 2}
        iconFilled
        label="Edit Stream Group"
        onClick={() => setShowOverlay(true)}
        tooltip="Edit Stream Group"
      />
    </>
  );
};

StreamGroupEditDialog.displayName = 'StreamGroupEditDialog';

export default memo(StreamGroupEditDialog);
