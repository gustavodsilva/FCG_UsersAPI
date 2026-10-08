Feature: User registration

  As a new user
  I want to register on the FIAP Cloud Games platform
  So that I can access the platform

  Scenario: Register user with valid data
    Given I provide valid user registration data
    When I request the user registration
    Then the user should be registered successfully
    And the registered user should have the Client profile

  Scenario: Reject registration with invalid email
    Given I provide user registration data with invalid email
    When I request the user registration
    Then the registration should fail with the message "E-mail inválido."

  Scenario: Reject registration with password shorter than eight characters
    Given I provide user registration data with a short password
    When I request the user registration
    Then the registration should fail with the message "A senha deve possuir no mínimo 8 caracteres."

  Scenario: Reject registration with password without letters
    Given I provide user registration data with a password without letters
    When I request the user registration
    Then the registration should fail with the message "A senha deve possuir pelo menos uma letra."

  Scenario: Reject registration with password without numbers
    Given I provide user registration data with a password without numbers
    When I request the user registration
    Then the registration should fail with the message "A senha deve possuir pelo menos um número."

  Scenario: Reject registration with password without special characters
    Given I provide user registration data with a password without special characters
    When I request the user registration
    Then the registration should fail with the message "A senha deve possuir pelo menos um caractere especial."

  Scenario: Reject registration with duplicated login
    Given I provide user registration data with an already registered login
    When I request the user registration
    Then the registration should fail with the message "Login já cadastrado."

  Scenario: Reject registration with duplicated email
    Given I provide user registration data with an already registered email
    When I request the user registration
    Then the registration should fail with the message "E-mail já cadastrado."

 Scenario: Store the user password securely
  Given I provide valid user registration data
  When I request the user registration
  Then the password should not be stored as plain text