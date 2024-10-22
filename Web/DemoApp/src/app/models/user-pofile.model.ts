import { IUserProfile } from "../interfaces/user-profile.interface";

export class UserProfile implements IUserProfile {
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];  
  permissions: string[];

  constructor(email: string, firstname: string, lastname: string, roles: string[], permissions: string[] ) {
    this.email = email;
    this.firstName = firstname;
    this.lastName = lastname;
    this.roles = roles;
    this.permissions = permissions;
  }
}

export class UserClaims {
  type: string;
  value: string;

  constructor(type: string, value: string) {
    this.type = type;
    this.value = value;
  }
}
