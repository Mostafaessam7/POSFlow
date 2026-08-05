export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  refreshToken: string;
  userId: string;
  tenantId: string;
  branchId: string | null;
  displayName: string;
  role: string;
}

export interface ForgotPasswordRequest {
  username: string;
}

export interface ResetPasswordWithTokenRequest {
  token: string;
  newPassword: string;
}