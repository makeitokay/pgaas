#!/bin/sh
set -o errexit

CLUSTER_NAME="pgaas"

if kind get clusters | grep -q "$CLUSTER_NAME"; then
    echo "Cluster '$CLUSTER_NAME' already exists."
else
   cat <<EOF | kind create cluster --name $CLUSTER_NAME --config=-
   kind: Cluster
   apiVersion: kind.x-k8s.io/v1alpha4
   nodes:
   - role: control-plane
     extraPortMappings:
     - containerPort: 6443
       hostPort: 6443
       protocol: TCP
     kubeadmConfigPatches:
     - |
       kind: ClusterConfiguration
       apiServer:
         certSANs:
           - 10.96.0.1
           - 172.18.0.2
           - 127.0.0.1
           - 0.0.0.0
           - 158.160.32.196
EOF
fi

kind export kubeconfig -n $CLUSTER_NAME

kubectl apply --server-side -f \
  https://raw.githubusercontent.com/cloudnative-pg/cloudnative-pg/release-1.25/releases/cnpg-1.25.0.yaml

kubectl apply --server-side -f https://github.com/fluxcd/flux2/releases/latest/download/install.yaml

cat <<EOF | kubectl apply -f -
---
apiVersion: v1
kind: Secret
metadata:
  name: pgaas-oci-creds
  namespace: default
stringData:
  username: "<username_here>"
  password: "<token_here>"
---
apiVersion: source.toolkit.fluxcd.io/v1
kind: HelmRepository
metadata:
  name: pgaas
  namespace: default
spec:
  type: "oci"
  interval: 5m0s
  url: oci://ghcr.io/makeitokay/pgaas
  secretRef:
    name: pgaas-oci-creds
EOF