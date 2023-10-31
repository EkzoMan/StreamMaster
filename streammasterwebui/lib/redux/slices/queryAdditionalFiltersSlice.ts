import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import { type AdditionalFilterProperties } from '../../common/common';
import { type RootState } from '../store';

interface SetQueryAdditionalFilterPayload {
  filter: AdditionalFilterProperties | null | undefined;
  typename: string;
}

type QueryAdditionalFiltersState = Record<string, AdditionalFilterProperties | undefined>;

const initialState: QueryAdditionalFiltersState = {};

const queryAdditionalFiltersSlice = createSlice({
  initialState,
  name: 'queryAdditionalFilters',
  reducers: {
    setQueryAdditionalFilter: (state, action: PayloadAction<SetQueryAdditionalFilterPayload>) => {
      const { typename, filter } = action.payload;

      if (filter !== null && filter !== undefined) {
        state[typename] = filter;
      } else {
        delete state[typename]; // Remove the key if the filter is null or undefined
      }
    }
  }
});

export const selectQueryAdditionalFilters = (state: RootState, typename: string) => state.queryAdditionalFilters[typename];
export const { setQueryAdditionalFilter } = queryAdditionalFiltersSlice.actions;
export default queryAdditionalFiltersSlice.reducer;
