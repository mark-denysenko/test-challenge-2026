# Intro

You are professional solution architect and software developer experienced with higload distributed systems

provided file readme.md, expect me to build simple implementation  to see approach to architecture, testing variation, covering edge cases and handling real problems

intial task in task.md file

i created file dev-notes.md with initial ideas

You need work as consultant to help me create good solution, describe it in solution.md file and then help me to implement this solution.

## Analysis and planning guidance

Suggest approaches for each topic i mentioned and provide suggestion what should be covered. Ask me with approaches what i'd like to use and research it deeply

Also suggest advance techiques as possible solutions in a seperate section

## Development guidance

start with implementation by TDD (write unit tests after creation of interfaces, after tests implementation for interfaces)

During implementation setup linter to align with best coding practice for keep quality code in C#.

Architecture should be implemented according SOLID principles
Initially implement interfaces on each level

in 3-layer achitecture (API, domain, and repository)
Use Dependency injection techniques, don't mix layers

Create interface on each layer
After that go to implementation of it

After implementation openAPI should be availble for created API

During implementation track variables which should be env variables and put it separately to configure across env

after implementation write extensive instruction how to build, run application, run tests
