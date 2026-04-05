// Re-export platform-agnostic Automation types so Automation.Link code
// Re-export platform-agnostic Automation types so Automation.Link code
// can reference them without fully-qualified names or per-file usings.
// This replaces the types that were previously duplicated in Automation.Link.
global using LantanaGroup.Automation.Helpers;
global using LantanaGroup.Automation.Configuration;
global using AutomationInvariant = LantanaGroup.Automation.Helpers.AutomationInvariant;
global using IAutomationOutput = LantanaGroup.Automation.Helpers.IAutomationOutput;
global using EventingAutomationOutput = LantanaGroup.Automation.Helpers.EventingAutomationOutput;
global using DualOutputHelper = LantanaGroup.Automation.Helpers.DualOutputHelper;
global using ConsoleAutomationOutput = LantanaGroup.Automation.Helpers.ConsoleAutomationOutput;
global using RetryHelper = LantanaGroup.Automation.Helpers.RetryHelper;
global using OAuthConfig = LantanaGroup.Automation.Configuration.OAuthConfig;
global using BasicAuthConfig = LantanaGroup.Automation.Configuration.BasicAuthConfig;
global using AuthHelper = LantanaGroup.Automation.AuthHelper;
