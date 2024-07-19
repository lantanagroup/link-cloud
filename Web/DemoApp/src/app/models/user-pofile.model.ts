import { IUserProfile } from "../interfaces/user-profile.interface";

export class UserProfile implements IUserProfile {
  email: string;
  firstName: string;
  lastName: string;
  facilities: string[];
  groups: string[];
  roles: string[];  

  constructor(email: string, firstname: string, lastname: string, facilities: string[], groups: string[], roles: string[] ) {
    this.email = email;
    this.firstName = firstname;
    this.lastName = lastname;
    this.facilities = facilities;
    this.groups = groups;
    this.roles = roles;
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
