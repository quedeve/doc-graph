# System Architecture Specification

## Introduction
This document describes the high-level architecture of the identity and access management system.

## Authentication Overview
Authentication is handled via multi-factor authentication (MFA) challenges and JWT bearer tokens.

### MFA Flow
1. User enters primary credentials (username and password).
2. The authentication service issues a time-based one-time password (TOTP) challenge.
3. Upon verification, an encrypted session token is signed and returned.

### Token Expiration
Tokens have a standard lifespan of 15 minutes with rolling refresh windows.
