--Set variables
DECLARE @LinkPermissionClaimType varchar(128);
DECLARE @IsLinkAdminClaim varchar(128)
DECLARE @LinkUserRoleId uniqueidentifier
DECLARE @UserId uniqueidentifier

SET @LinkPermissionClaimType = 'permissions'
SET @IsLinkAdminClaim = 'IsLinkAdmin'
SET @LinkUserRoleId = NEWID()

-- Add LinkUser role
INSERT INTO Roles(Id, Name, Description, CreatedOn) VALUES(@LinkUserRoleId, 'LinkUser', 'A user of the Link application.', GETDATE())
INSERT INTO RoleClaims(RoleId, ClaimType, ClaimValue) VALUES(@LinkUserRoleId, @LinkPermissionClaimType, @IsLinkAdminClaim)

-- Add Seed Users
--SET @UserId = NewID()
--INSERT INTO Users(Id, Email, FirstName, LastName, CreatedOn, IsActive, IsDeleted) VALUES(@UserId, '<insertemail>', '<insertfirstname>', '<insertlastname>', GETDATE(), 1, 0)
--INSERT INTO UserRoles(UserId, RoleId) VALUES(@UserId, @LinkUserRoleId)