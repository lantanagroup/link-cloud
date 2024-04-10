Feature: Create an AuditEvent
    In order to log an audit event in my application
    As a developer
    I want to be able to create an AuditEvent with the values provided by a received AuditEventMessage

Scenario: Create an AuditEvent with the properties of a received AuditEventMessage
    Given I have received an AuditEventMessage 
    When I create a new AuditEvent based on this AuditEventMessage
    Then the Id property should be a valid unique identifier
    And the other properties of the AuditEvent should match those of the received AuditEventMessage

