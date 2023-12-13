export const environment = {
  production: true,
  baseApiUrl: "http://localhost:7777",
  idpIssuer: "",
  idpClientId: '',
  idpClientSecret: '',
  idpScope: 'openid profile email botwdemogatewayapi.read botwdemogatewayapi.write',
  redirectUri: window.location.origin + '/',
  loginUrl: window.location.origin + '/',
};
