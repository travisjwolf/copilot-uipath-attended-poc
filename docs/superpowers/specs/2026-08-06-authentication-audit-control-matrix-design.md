# Authentication and Audit Control Matrix Design

- **Status:** Approved on August 6, 2026
- **Primary audience:** Proof-of-concept sponsors
- **Deliverable:** `docs/security/authentication-audit-control-matrix.md`

## Purpose

Document the authentication, authorization, identity-attribution, correlation,
and audit controls at every handoff in the Copilot-to-UiPath architecture. The
assessment must distinguish successful automation execution from proof that a
specific authenticated person initiated that execution.

## Approach

Use a technical control matrix rather than a narrative risk memo. Each row
covers one component boundary and identifies the current authentication
mechanism, the principal visible downstream, available audit evidence, the
remaining blocker, and its priority. A short decision summary gives sponsors a
clear assurance statement before the technical detail.

The document will:

- Cover Copilot Studio, Power Automate, the on-premises data gateway, the local
  ASP.NET Core bridge, UiPath RobotAPI, and the interactive Windows session.
- Treat `requestedBy` as unverified metadata because the current caller supplies
  it and the bridge does not bind it to an authenticated principal.
- Separate native logs in each platform from an end-to-end, correlated audit
  record.
- Identify current repository controls without presenting them as user-level
  authentication or authorization.
- Define acceptance gates and accountable owner groups for closing the gaps.
- Use only synthetic identifiers and avoid credentials or production data.

## Evidence Standard

Repository code and setup records are the source of truth for implemented
behavior. Current Microsoft and UiPath documentation supports platform-level
capability statements. The installed `uip` CLI command surface is used to
describe UiPath audit discovery, query, and export; tenant evidence must be
validated separately with an authorized session.
