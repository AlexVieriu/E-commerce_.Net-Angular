// A real Error (stack, name, message) that carries the server's validation messages.
export class ValidationError extends Error {
  constructor(public readonly errors: string[]) {
    super('Validation failed');
    this.name = 'ValidationError';
  }
}
