-- Deletes one user and everything that depends on them.
-- Replace someone@acme.com with the user's email in every statement (Find/Replace All),
-- then run the statements in order. Step 2 must run before step 4.
--
-- To be able to undo: switch DBeaver to manual commit before step 2, run steps 2-4,
-- check the row counts, then Commit (or Rollback).
--
-- This doesn't touch the Identity Platform account: staff are re-created as ScrapGo User
-- at their next Google sign-in. Delete or disable the account in Identity Platform > Users
-- to stop that.

-- 1. Check who will be deleted (and their roles). Expect exactly one row.
--    If they hold site_admin, make sure another active Site Admin exists first.
SELECT u.id, u.email, u.user_type, u.status, string_agg(r.key, ', ') AS roles
FROM identity.users u
LEFT JOIN identity.user_roles ur ON ur.user_id = u.id
LEFT JOIN identity.roles r ON r.id = ur.role_id
WHERE lower(u.email) = lower('someone@acme.com')
GROUP BY u.id, u.email, u.user_type, u.status;

-- 2. Delete their audit rows
DELETE FROM audit.audit_logs
WHERE user_id IN (SELECT id FROM identity.users WHERE lower(email) = lower('someone@acme.com'))
   OR metadata ->> 'uid' IN (SELECT identity_platform_uid FROM identity.users WHERE lower(email) = lower('someone@acme.com'))
   OR lower(metadata ->> 'email') = lower('someone@acme.com');

-- 3. Delete invitations they sent, and invitations to their email
DELETE FROM identity.invitations
WHERE invited_by_user_id IN (SELECT id FROM identity.users WHERE lower(email) = lower('someone@acme.com'))
   OR lower(email) = lower('someone@acme.com');

-- 4. Delete the user (their roles, supplier links and feature targets go with it)
DELETE FROM identity.users
WHERE lower(email) = lower('someone@acme.com');
