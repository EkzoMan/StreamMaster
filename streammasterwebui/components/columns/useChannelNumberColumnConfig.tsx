import ChannelNumberEditor from '../ChannelNumberEditor';
import createMultiSelectColumnConfigHook from './createMultiSelectColumnConfigHook';

export const useChannelNumberColumnConfig = createMultiSelectColumnConfigHook({
  EditorComponent: ChannelNumberEditor,
  dataField: 'user_Tvg_chno',
  headerTitle: 'Ch.',
  useFilter: false,
  width: 4
});
