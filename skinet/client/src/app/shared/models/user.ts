import { Address } from './address';

// Optional like /user-info: names are `string?` and the address can be null.
// Declared required, `null + ' ' + null` compiled and showed "null null" (BUG-13).
export type User = {
  firstName?: string;
  lastName?: string;
  email: string;
  address?: Address;
  roles: string[];
};
