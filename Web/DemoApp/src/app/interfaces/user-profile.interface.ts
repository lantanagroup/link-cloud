export interface IUserProfile {
  username: string;
  firstName: string;
  lastName: string;
  facilities: string[],
  groups: string[]; //switch to group model
  roles: string[]; //switch to role model
}
