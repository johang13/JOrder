# JOrder.Identity

## Local JWT key files

For local development, generate an RSA private key under `keys/` so `Authentication:JwtSigning:PrivateKeyPath` can load `keys/signing-key.pem`.

```bash
cd /Users/chris/repos/JOrder/src/JOrder.Identity
mkdir -p keys
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out keys/signing-key.pem
chmod 600 keys/signing-key.pem
```

Other services validate tokens through OIDC discovery/JWKS, you do not need to manually export a public key file.

Optional: verify the private key format.

```bash
openssl pkey -in keys/signing-key.pem -check -noout
```

Finally, create a Kubernetes secret for the private key, so it can be mounted in the container.

```bash
kubectl create namespace jorder
kubectl create secret generic identity-signing-key \
  --from-file=signing-key.pem=src/JOrder.Identity/keys/signing-key.pem \
  -n jorder
```