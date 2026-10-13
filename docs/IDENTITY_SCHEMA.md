# ASP.NET Core Identity Database Schema & Architecture Guide

This guide documents the relational schema, tables, foreign key relationships, and functional responsibilities of ASP.NET Core Identity within the **Personal Finance App**.

---

## 1. Schema Overview & Relational Model

When ASP.NET Core Identity is integrated with Entity Framework Core (`IdentityDbContext`), it provisions a normalized relational model consisting of **7 core tables** prefixed with `AspNet`. 

This modular design isolates identity storage, external authentication links, role-based authorization, fine-grained claims, and security tokens to optimize maintainability, data integrity, and authentication throughput.

```
                  ┌──────────────────────┐
                  │     AspNetRoles      │
                  └──────────┬───────────┘
                             │
            ┌────────────────┼────────────────┐
            │ 1:N            │ M:N            │ 1:N
            ▼                ▼                ▼
   ┌─────────────────┐ ┌───────────────┐ ┌─────────────────┐
   │ AspNetRoleClaims│ │AspNetUserRoles│ │  AspNetUsers    │
   └─────────────────┘ └───────┬───────┘ └────────┬────────┘
                               │                  │
                               └─────────┬────────┘
                                         │
            ┌────────────────────────────┼────────────────────────────┐
            │ 1:N                        │ 1:N                        │ 1:N
            ▼                            ▼                            ▼
   ┌─────────────────┐          ┌─────────────────┐          ┌─────────────────┐
   │AspNetUserClaims │          │ AspNetUserLogins│          │ AspNetUserTokens│
   └─────────────────┘          └─────────────────┘          └─────────────────┘
```

---

## 2. Core Identity Tables Breakdown

### 1. `AspNetUsers` (Core User Accounts)
Represents the primary user record for each registered account in the system.

* **Primary Key**: `Id` (`nvarchar(450)` / string GUID)
* **Key Columns**:
  * `UserName` & `NormalizedUserName`: Display name and uppercase indexed equivalent for case-insensitive lookup.
  * `Email` & `NormalizedEmail`: User email address and normalized uppercase equivalent for duplicate validation and index lookups.
  * `EmailConfirmed` (`bit` / `boolean`): Indicates whether the user verified ownership of their email address (`1` = verified, `0` = unconfirmed).
  * `PasswordHash`: PBKDF2 cryptographic hash of the local password (`NULL` if the account was created exclusively via external OAuth providers such as Google).
  * `SecurityStamp`: Random cryptographic stamp updated whenever credentials change (e.g., password reset, adding or removing logins). Used to invalidate stale active sessions on other devices.
  * `ConcurrencyStamp`: Optimistic concurrency token preventing conflicting concurrent writes.
  * `PhoneNumber` & `PhoneNumberConfirmed`: Phone contact data and confirmation flag for SMS/2FA workflows.
  * `TwoFactorEnabled` (`bit`): Indicates if two-factor authentication is mandatory for this user.
  * `LockoutEnd`, `LockoutEnabled`, `AccessFailedCount`: Brute-force mitigation tracking failed sign-in attempts and lock status.

---

### 2. `AspNetRoles` (Authorization Roles)
Defines high-level permission groupings (such as `Admin`, `Auditor`, or `Member`) used for role-based access control (`[Authorize(Roles = "Admin")]`).

* **Primary Key**: `Id` (`nvarchar(450)` / string GUID)
* **Key Columns**:
  * `Name`: The human-readable role name (e.g., `Admin`).
  * `NormalizedName`: Uppercase role identifier used for indexing and quick lookups (e.g., `ADMIN`).
  * `ConcurrencyStamp`: Optimistic concurrency token for role updates.

---

### 3. `AspNetUserRoles` (User-to-Role Mapping)
A join table implementing the many-to-many relationship between `AspNetUsers` and `AspNetRoles`.

* **Primary Key**: Composite `(UserId, RoleId)`
* **Foreign Keys**:
  * `UserId` &rarr; `AspNetUsers.Id` (Cascade delete)
  * `RoleId` &rarr; `AspNetRoles.Id` (Cascade delete)
* **Purpose**: Allows a single user to hold multiple roles simultaneously, and a single role to be assigned to multiple users without data duplication.

---

### 4. `AspNetUserClaims` (User-Specific Claims)
Stores discrete key-value attribute assertions assigned directly to a specific user account.

* **Primary Key**: `Id` (`int` identity / auto-increment)
* **Foreign Key**: `UserId` &rarr; `AspNetUsers.Id` (Cascade delete)
* **Key Columns**:
  * `ClaimType`: Identifier URI/name of the claim (e.g., `urn:google:picture`, `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname`, `Department`).
  * `ClaimValue`: The raw string value (e.g., avatar image URL, display name, department ID).
* **Application Usage**:
  * Stores Google profile metadata synchronized on login via `SynchronizeCustomClaimsAsync` (`urn:google:picture`, `ClaimTypes.GivenName`, `urn:google:locale`).
  * Claims are loaded into the user's `ClaimsPrincipal` upon sign-in.

---

### 5. `AspNetRoleClaims` (Role-Level Claims)
Stores claims attached to an entire role rather than an individual user.

* **Primary Key**: `Id` (`int` identity / auto-increment)
* **Foreign Key**: `RoleId` &rarr; `AspNetRoles.Id` (Cascade delete)
* **Key Columns**:
  * `ClaimType`: The permission or policy descriptor (e.g., `Permission`).
  * `ClaimValue`: The granular permission value (e.g., `Reports.Export`, `Users.Manage`).
* **Purpose**: Enables claim-based authorization across role members without creating redundant rows for every individual user in `AspNetUserClaims`.

---

### 6. `AspNetUserLogins` (External & Federated Logins)
Maps external OAuth 2.0 / OpenID Connect identity providers (e.g., Google, GitHub, Microsoft) to the internal `AspNetUsers` account.

* **Primary Key**: Composite `(LoginProvider, ProviderKey)`
* **Foreign Key**: `UserId` &rarr; `AspNetUsers.Id` (Cascade delete)
* **Key Columns**:
  * `LoginProvider`: Unique provider identifier (e.g., `"Google"`).
  * `ProviderKey`: The unique user/subject ID issued by the third-party provider.
  * `ProviderDisplayName`: Friendly display name for UI rendering (e.g., `"Google"`).
* **Application Usage**:
  * When a user clicks "Continue with Google", `ExternalLoginSignInAsync` queries this table using `LoginProvider` and `ProviderKey` to authenticate the matching `AspNetUsers` entity.

---

### 7. `AspNetUserTokens` (Security & Integration Tokens)
Persists temporary tokens, authenticator keys, and external provider tokens.

* **Primary Key**: Composite `(UserId, LoginProvider, Name)`
* **Foreign Key**: `UserId` &rarr; `AspNetUsers.Id` (Cascade delete)
* **Key Columns**:
  * `LoginProvider`: Issuer or context identifier (e.g., `[AspNetUserStore]`, `"Google"`).
  * `Name`: Token purpose descriptor (e.g., `"AuthenticatorKey"` for TOTP 2FA, `"RecoveryCodes"`, `"access_token"`).
  * `Value`: Encrypted/hashed token payload.
* **Purpose**: Manages 2FA shared secrets, recovery codes, and OAuth access/refresh tokens when token persistence is enabled.

---

## 3. Quick Reference Matrix

| Table Name | Primary Responsibility | Cardinality | Key Use Cases |
|---|---|---|---|
| `AspNetUsers` | User Identity & Account State | 1 (Root) | Password authentication, account lockout, email verification status. |
| `AspNetRoles` | Authorization Groups | 1 (Root) | Grouping permissions under roles like `Admin` or `User`. |
| `AspNetUserRoles` | User-to-Role Association | M:N | Assigning users to one or more authorization roles. |
| `AspNetUserClaims` | User Attributes / Metadata | 1:N to User | Storing user avatar URLs, given names, locale preferences. |
| `AspNetRoleClaims` | Role Permissions | 1:N to Role | Granular policy claims inherited by all users in a role. |
| `AspNetUserLogins` | External OAuth Provider Links | 1:N to User | Associating Google, GitHub, or Microsoft IDs with local accounts. |
| `AspNetUserTokens` | Security & 2FA State | 1:N to User | Storing TOTP 2FA authenticator keys and password reset tokens. |

---

## 4. Interaction with Application-Specific Tables

In `PersonalFinance.Data.AppDbContext`, the application extends Identity with domain-specific entities:

```
┌─────────────────┐       1:N        ┌─────────────────────────┐       1:N        ┌───────────────────────────┐
│  AspNetUsers    ├─────────────────►│  GoogleDriveConnections │─────────────────►│   GoogleDriveCachedFiles  │
└─────────────────┘                  └─────────────────────────┘                  └───────────────────────────┘
```

1. **`GoogleDriveConnections`**:
   - Linked to `AspNetUsers.Id` via `UserId` foreign key/index.
   - Holds user-configured Google Drive folder connections, encrypted API keys (AES-256), and sync statuses.
2. **`GoogleDriveCachedFiles`**:
   - Linked to `GoogleDriveConnections.Id` via `ConnectionId` with cascade delete.
   - Caches Google Drive file metadata, thumbnail links, MIME types, and financial spreadsheet structure.
3. **`Items`**:
   - Application domain items and transaction entities tracked by the system.
