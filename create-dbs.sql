IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'link-audit') CREATE DATABASE [link-audit];
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'link-census') CREATE DATABASE [link-census];
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'link-dataacquisition') CREATE DATABASE [link-dataacquisition];
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'link-normalization') CREATE DATABASE [link-normalization];
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'link-tenant') CREATE DATABASE [link-tenant];