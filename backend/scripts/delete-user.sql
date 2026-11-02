-- Deletes one user from the identity database, with everything that depends on them.
--
-- Dry run (default): shows the user and what would go, then rolls back.
--   psql -v ON_ERROR_STOP=1 -v email=someone@acme.com -f scripts/delete-user.sql
-- Delete for real: add -v confirm=yes. Select by GCIP UID instead with -v uid=<uid>.
--
-- In one transaction it deletes:
--   - invitations the user sent (invited_by_user_id would otherwise block the delete)
--   - invitations to the user's email
--   - the user's audit rows: their own events, and events about their UID or email
--   - the user row; the database then removes their roles, supplier links and
--     feature targets (ON DELETE CASCADE) and clears assigned_by, linked_by,
--     updated_by and accepted_by on other rows (ON DELETE SET NULL)
-- It refuses to delete the last active Site Admin.
--
-- Their Identity Platform sign-in account is NOT touched: staff are re-created as
-- ScrapGo User at their next Google sign-in, and external users get no_access until
-- invited again. Delete or disable the account in Identity Platform > Users to stop that.

\set ON_ERROR_STOP on
\if :{?email}
\else
    \set email ''
\endif
\if :{?uid}
\else
    \set uid ''
\endif
\if :{?confirm}
\else
    \set confirm no
\endif

SELECT (:'email' <> '') <> (:'uid' <> '') AS one_selector \gset
\if :one_selector
\else
    \echo 'Pass exactly one of -v email=<email> or -v uid=<identity platform uid>.'
    \quit
\endif

BEGIN;

CREATE TEMP TABLE target ON COMMIT DROP AS
SELECT id, email, identity_platform_uid, user_type, status
FROM identity.users
WHERE (:'email' <> '' AND lower(email) = lower(:'email'))
   OR (:'uid' <> '' AND identity_platform_uid = :'uid');

DO $$
DECLARE
    matches int;
BEGIN
    SELECT count(*) INTO matches FROM pg_temp.target;
    IF matches = 0 THEN
        RAISE EXCEPTION 'No user matches. Nothing was changed.';
    END IF;
    IF matches > 1 THEN
        RAISE EXCEPTION '% users match that email; select one with -v uid=<uid>. Nothing was changed.', matches;
    END IF;

    -- Without an active Site Admin only the bootstrap-site-admin command gets anyone back in.
    IF EXISTS (
        SELECT 1
        FROM identity.user_roles ur
        JOIN identity.roles r ON r.id = ur.role_id
        WHERE r.key = 'site_admin' AND ur.user_id IN (SELECT id FROM pg_temp.target))
       AND NOT EXISTS (
        SELECT 1
        FROM identity.user_roles ur
        JOIN identity.roles r ON r.id = ur.role_id
        JOIN identity.users u ON u.id = ur.user_id
        WHERE r.key = 'site_admin' AND u.status = 'Active' AND u.id NOT IN (SELECT id FROM pg_temp.target))
    THEN
        RAISE EXCEPTION 'This is the last active Site Admin. Give another user site_admin first. Nothing was changed.';
    END IF;
END $$;

\echo
\echo 'User:'
SELECT t.id, t.email, t.identity_platform_uid AS uid, t.user_type, t.status,
       coalesce(string_agg(r.key, ', ' ORDER BY r.key), '(none)') AS roles
FROM target t
LEFT JOIN identity.user_roles ur ON ur.user_id = t.id
LEFT JOIN identity.roles r ON r.id = ur.role_id
GROUP BY t.id, t.email, t.identity_platform_uid, t.user_type, t.status;

\echo 'Rows affected:'
SELECT 'deleted' AS action, 'identity.user_roles (their roles)' AS rows_in, count(*) AS count
    FROM identity.user_roles WHERE user_id IN (SELECT id FROM target)
UNION ALL SELECT 'deleted', 'identity.user_suppliers (their supplier links)', count(*)
    FROM identity.user_suppliers WHERE user_id IN (SELECT id FROM target)
UNION ALL SELECT 'deleted', 'identity.feature_targets (features targeted at them)', count(*)
    FROM identity.feature_targets WHERE user_id IN (SELECT id FROM target)
UNION ALL SELECT 'deleted', 'identity.invitations (sent by them)', count(*)
    FROM identity.invitations WHERE invited_by_user_id IN (SELECT id FROM target)
UNION ALL SELECT 'deleted', 'identity.invitations (to their email)', count(*)
    FROM identity.invitations i WHERE lower(i.email) IN (SELECT lower(email) FROM target)
UNION ALL SELECT 'deleted', 'audit.audit_logs (by or about them)', count(*)
    FROM audit.audit_logs a
    WHERE a.user_id IN (SELECT id FROM target)
       OR a.metadata ->> 'uid' IN (SELECT identity_platform_uid FROM target)
       OR lower(a.metadata ->> 'email') IN (SELECT lower(email) FROM target)
UNION ALL SELECT 'set null', 'identity.user_roles.assigned_by_user_id', count(*)
    FROM identity.user_roles WHERE assigned_by_user_id IN (SELECT id FROM target)
UNION ALL SELECT 'set null', 'identity.user_suppliers.linked_by_user_id', count(*)
    FROM identity.user_suppliers WHERE linked_by_user_id IN (SELECT id FROM target)
UNION ALL SELECT 'set null', 'identity.features.updated_by_user_id', count(*)
    FROM identity.features WHERE updated_by_user_id IN (SELECT id FROM target)
UNION ALL SELECT 'set null', 'identity.invitations.accepted_by_user_id', count(*)
    FROM identity.invitations WHERE accepted_by_user_id IN (SELECT id FROM target);

-- Explicit deletes first; the user row's cascades and SET NULLs handle the rest.
DELETE FROM identity.invitations WHERE invited_by_user_id IN (SELECT id FROM target);

DELETE FROM identity.invitations WHERE lower(email) IN (SELECT lower(email) FROM target);

DELETE FROM audit.audit_logs a
WHERE a.user_id IN (SELECT id FROM target)
   OR a.metadata ->> 'uid' IN (SELECT identity_platform_uid FROM target)
   OR lower(a.metadata ->> 'email') IN (SELECT lower(email) FROM target);

DELETE FROM identity.users WHERE id IN (SELECT id FROM target);

\if :confirm
    COMMIT;
    \echo
    \echo 'Deleted. Their Identity Platform account still exists: delete or disable it there to stop them signing in again.'
\else
    ROLLBACK;
    \echo
    \echo 'Dry run: nothing was changed. Run again with -v confirm=yes to delete.'
\endif
