import { DynamicPayload, DynamicResponse } from "../types";

export interface Model {
  id: string;
  createdAt: Date;
  updatedAt: Date;
  payload: DynamicPayload;
  response: DynamicResponse;
}
