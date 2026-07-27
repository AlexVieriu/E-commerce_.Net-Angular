// Custom Error subclass for ASP.NET model validation failures.
// Carries the array of validation messages while still being a proper Error object
// (has .stack, .message, .name — unlike throwing a plain array).
export class ValidationError extends Error {
  constructor(public readonly errors: string[]) {
    super('Validation failed');
    this.name = 'ValidationError';
  }
}
