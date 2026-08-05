export interface UserResponse {
  id: string;
  username: string;
  displayName: string;
  email: string | null;
  role: string;
  branchId: string | null;
  isActive: boolean;
}

export interface CreateUserRequest {
  username: string;
  displayName: string;
  email: string | null;
  password: string;
  role: string;
  branchId: string | null;
}

export interface UpdateUserRequest {
  displayName: string;
  email: string | null;
  role: string;
  branchId: string | null;
  isActive: boolean;
}

export interface ResetPasswordRequest {
  newPassword: string;
}
