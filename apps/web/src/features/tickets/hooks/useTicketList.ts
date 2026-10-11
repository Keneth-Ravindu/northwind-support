
import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [debouncedSearch, setDebouncedSearch] = useState(filters.search);
  const [state, setState] = useState<State>({ isLoading: true });
  const requestId = useRef(0);

  // Debounce only the search input.
  useEffect(() => {
    const handle = window.setTimeout(() => {
      setDebouncedSearch(filters.search);
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [filters.search]);

  // All filter changes automatically trigger this effect.
  // Search uses its debounced value.
  useEffect(() => {
    // Do not issue a request with stale search criteria.
    if (debouncedSearch !== filters.search) {
      return;
    }

    const currentRequest = ++requestId.current;
    const query = { ...filters, search: debouncedSearch };

    setState((current) => ({
      ...current,
      isLoading: true,
      error: undefined,
    }));

    ticketsApi.getTickets(query)
      .then((data) => {
        if (currentRequest === requestId.current) {
          setState({ isLoading: false, data, error: undefined });
        }
      })
      .catch((error: unknown) => {
        if (currentRequest === requestId.current) {
          const message =
            error instanceof ApiError
              ? error.message
              : 'Unable to load tickets.';

          setState((current) => ({
            ...current,
            isLoading: false,
            error: message,
          }));
        }
      });

    return () => {
      requestId.current++;
    };
  }, [filters, debouncedSearch]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({
      ...current,
      ...patch,
      page: patch.page ?? 1,
    }));

  const resetFilters = () => setFilters({ ...emptyFilters });

  return {
    filters,
    updateFilters,
    resetFilters,
    reload: () => {
      setFilters((current) => ({ ...current }));
    },
    ...state,
  };
}
