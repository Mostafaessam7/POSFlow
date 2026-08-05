namespace PosFlow.Application.Auth;

/// <summary>SecretKey is shown once so the user can type it manually if they can't scan a QR; the frontend renders OtpAuthUri as a QR code (any otpauth:// QR renderer works client-side, no server-side image generation needed).</summary>
public sealed record TwoFactorSetupResponse(
    string SecretKey,
    string OtpAuthUri
);
