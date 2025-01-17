#!/bin/sh
set -o errexit

CLUSTER_NAME="pgaas"

reg_name='kind-registry'
reg_port='5001'
if [ "$(docker inspect -f '{{.State.Running}}' "${reg_name}" 2>/dev/null || true)" != 'true' ]; then
  docker run \
    -d --restart=always -p "127.0.0.1:${reg_port}:5000" --network bridge --name "${reg_name}" \
    registry:2
fi

if kind get clusters | grep -q "$CLUSTER_NAME"; then
    echo "Cluster '$CLUSTER_NAME' already exists."
else
    cat <<EOF | kind create cluster --name $CLUSTER_NAME --config=-
    kind: Cluster
    apiVersion: kind.x-k8s.io/v1alpha4
    containerdConfigPatches:
    - |-
      [plugins."io.containerd.grpc.v1.cri".registry.mirrors."kind-registry:5000"]
        endpoint = ["http://kind-registry:5000"]
      [plugins."io.containerd.grpc.v1.cri".registry.configs."kind-registry:5000".tls]
        insecure_skip_verify = true
EOF
fi

kind export kubeconfig -n $CLUSTER_NAME

if [ "$(docker inspect -f='{{json .NetworkSettings.Networks.kind}}' "${reg_name}")" = 'null' ]; then
  docker network connect "kind" "${reg_name}"
fi

cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ConfigMap
metadata:
  name: local-registry-hosting
  namespace: kube-public
data:
  localRegistryHosting.v1: |
    host: "localhost:${reg_port}"
    help: "https://kind.sigs.k8s.io/docs/user/local-registry/"
EOF

kubectl apply --server-side -f \
  https://raw.githubusercontent.com/cloudnative-pg/cloudnative-pg/release-1.25/releases/cnpg-1.25.0.yaml

kubectl apply --server-side -f https://github.com/fluxcd/flux2/releases/latest/download/install.yaml

cat <<EOF | kubectl apply -f -
apiVersion: source.toolkit.fluxcd.io/v1
kind: HelmRepository
metadata:
  name: pgaas
  namespace: default
spec:
  type: "oci"
  interval: 5m0s
  url: oci://kind-registry:5000/pgaas
  insecure: true
EOF