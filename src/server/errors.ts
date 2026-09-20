export class AppError extends Error {
  readonly status: number;
  readonly code: string;
  readonly expose: boolean;
  readonly correlationId: string;

  constructor(code: string, message: string, status = 400, expose = true) {
    super(message);
    this.name = "AppError";
    this.code = code;
    this.status = status;
    this.expose = expose;
    this.correlationId = crypto.randomUUID();
  }
}

export class UnauthorizedError extends AppError {
  constructor(message = "You need to sign in to continue.") {
    super("UNAUTHORIZED", message, 401);
  }
}

export class ForbiddenError extends AppError {
  constructor(message = "You do not have permission to do that.") {
    super("FORBIDDEN", message, 403);
  }
}

export class NotFoundError extends AppError {
  constructor(entity = "Record") {
    super("NOT_FOUND", `${entity} was not found.`, 404);
  }
}

export class ConflictError extends AppError {
  constructor(message: string) {
    super("CONFLICT", message, 409);
  }
}

export class RateLimitError extends AppError {
  constructor(message = "Too many attempts. Please wait and try again.") {
    super("RATE_LIMIT", message, 429);
  }
}

export class ValidationError extends AppError {
  constructor(message: string) {
    super("VALIDATION", message, 422);
  }
}

export function toPublicError(error: unknown) {
  if (error instanceof AppError) {
    return {
      error: error.expose ? error.message : "Something went wrong.",
      code: error.code,
      correlationId: error.correlationId,
      status: error.status,
    };
  }
  const correlationId = crypto.randomUUID();
  console.error("Unhandled error", { correlationId, error });
  return {
    error: "Something went wrong. If this continues, contact your administrator with the reference below.",
    code: "INTERNAL",
    correlationId,
    status: 500,
  };
}
