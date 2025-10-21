export interface CorrelationCacheEntry {
  correlationId: string;
  errorMessage?: string | null;
}

export interface CorrelationCache {
  [topic: string]: CorrelationCacheEntry[];
}
