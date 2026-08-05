export interface BranchResponse {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
}

export interface CreateBranchRequest {
  name: string;
  code: string;
}

export interface UpdateBranchRequest {
  name: string;
  code: string;
  isActive: boolean;
}
