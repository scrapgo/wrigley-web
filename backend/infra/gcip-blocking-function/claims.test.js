// Run with: node --test
const test = require('node:test');
const assert = require('node:assert');
const { sessionClaimsFor } = require('./claims');

const google = (profile) => ({ additionalUserInfo: { providerId: 'google.com', profile } });

test('a Workspace Google sign-in gets its hd as a session claim', () => {
  assert.deepStrictEqual(sessionClaimsFor(google({ email: 'a@scrapgo.com', hd: 'scrapgo.com' })), { hd: 'scrapgo.com' });
});

test('hd is lower-cased', () => {
  assert.deepStrictEqual(sessionClaimsFor(google({ hd: 'ScrapGo.com' })), { hd: 'scrapgo.com' });
});

test('a personal Google account (no hd) gets no claim', () => {
  assert.deepStrictEqual(sessionClaimsFor(google({ email: 'a@gmail.com' })), {});
});

test('an email/password sign-in gets no claim', () => {
  assert.deepStrictEqual(sessionClaimsFor({ additionalUserInfo: { providerId: 'password' } }), {});
});

test('a non-Google provider claiming hd gets no claim', () => {
  assert.deepStrictEqual(
    sessionClaimsFor({ additionalUserInfo: { providerId: 'oidc.other', profile: { hd: 'scrapgo.com' } } }),
    {});
});

test('missing context gets no claim', () => {
  assert.deepStrictEqual(sessionClaimsFor(undefined), {});
  assert.deepStrictEqual(sessionClaimsFor({}), {});
});
