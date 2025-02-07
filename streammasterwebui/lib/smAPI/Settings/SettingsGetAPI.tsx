/* eslint unused-imports/no-unused-imports-ts: off */
/* eslint @typescript-eslint/no-unused-vars: off */
import { StringArgument } from '@lib/common/dataTypes';
import type * as iptv from '@lib/iptvApi';
import { invokeHubConnection } from '@lib/signalr/signalr';

export const GetIsSystemReady = async (): Promise<void | null> => {
  await invokeHubConnection<void>('GetIsSystemReady');
};
export const GetSetting = async (argument: iptv.SettingDto): Promise<iptv.SettingDto | null> =>
  invokeHubConnection<iptv.SettingDto>('GetSetting', argument);
export const GetSystemStatus = async (argument: iptv.SDSystemStatus): Promise<iptv.SDSystemStatus | null> =>
  invokeHubConnection<iptv.SDSystemStatus>('GetSystemStatus', argument);
export const LogIn = async (argument: iptv.LogInRequest): Promise<void | null> => {
  await invokeHubConnection<void>('LogIn', argument);
};
