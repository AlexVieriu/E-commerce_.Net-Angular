import { Address } from "./address";

// Nullability mirrors AccountController's /user-info: AppUser.FirstName/LastName are `string?`
// and Address comes from `user.Address?.toDto()`. Declaring them required is what let BUG-13
// through — `null + ' ' + null` compiles fine and yields the string "null null".
export type User = {
    firstName?: string;
    lastName?: string;
    email: string;
    address?: Address;
    roles: string[];
}