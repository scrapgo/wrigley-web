// GCIP beforeSignIn blocking function: copies the Google Workspace domain (hd)
// into the GCIP ID token as a session claim. See README.md.
const gcipCloudFunctions = require('gcip-cloud-functions');
const { sessionClaimsFor } = require('./claims');

const authClient = new gcipCloudFunctions.Auth();

exports.beforeSignIn = authClient.functions().beforeSignInHandler((user, context) => {
  const sessionClaims = sessionClaimsFor(context);

  return Object.keys(sessionClaims).length > 0 ? { sessionClaims } : {};
});
