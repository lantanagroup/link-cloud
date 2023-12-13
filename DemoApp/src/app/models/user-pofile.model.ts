import { IUserProfile } from "../interfaces/user-profile.interface";

export class UserProfile implements IUserProfile {
  username: string;
  firstName: string;
  lastName: string;
  facilities: string[];
  groups: string[];
  roles: string[];  

  constructor(username: string, firstname: string, lastname: string, facilities: string[], groups: string[], roles: string[] ) {
    this.username = username;
    this.firstName = firstname;
    this.lastName = lastname;
    this.facilities = facilities;
    this.groups = groups;
    this.roles = roles;
  }
}
