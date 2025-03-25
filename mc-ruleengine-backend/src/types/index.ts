export type DynamicPayload = Record<string, any>;

export interface DynamicResponse {
  status: string;
  data?: any;
  error?: string;
}

export interface ApiResponse<T = any> {
  success: boolean;
  message?: string;
  payload?: T;
}
