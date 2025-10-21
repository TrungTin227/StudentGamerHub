# Changelog

## Membership hierarchy APIs
- Added end-to-end community, club, and room membership flows (create, join, kick) with transactional safeguards.
- Controllers now return full detail DTOs and expose `/join` and `/members/{userId}` endpoints for community, club, and room management.
- Services rewrite aligns cache counters with membership tables and returns enriched DTO projections.
