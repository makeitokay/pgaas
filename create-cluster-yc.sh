#!/bin/sh
set -o errexit

CLUSTER_NAME="pgaas"

GITHUB_USERNAME = "<username_here>"
GITHUB_TOKEN = "<token_here>"

if kind get clusters | grep -q "$CLUSTER_NAME"; then
    echo "Cluster '$CLUSTER_NAME' already exists."
else
   cat <<EOF | kind create cluster --name $CLUSTER_NAME --config=-
   kind: Cluster
   apiVersion: kind.x-k8s.io/v1alpha4
   nodes:
   - role: control-plane
     extraPortMappings:
     - containerPort: 32090
       hostPort: 5432
       protocol: TCP
     - containerPort: 6443
       hostPort: 6443
       protocol: TCP
     - containerPort: 32091
       hostPort: 80
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
  username: "$GITHUB_USERNAME"
  password: "$GITHUB_TOKEN"
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

kubectl apply --server-side -f https://raw.githubusercontent.com/cloudnative-pg/postgres-containers/refs/heads/main/Debian/ClusterImageCatalog-bullseye.yaml

helm repo add traefik https://traefik.github.io/charts
helm repo update

helm install traefik traefik/traefik -n traefik --create-namespace \
  --set ports.pg.exposedPort=5432 --set ports.pg.port=5432
  
cat <<EOF | kubectl apply -f -
---
apiVersion: v1
kind: Service
metadata:
  name: traefik-pg
  namespace: traefik
spec:
  type: NodePort
  externalTrafficPolicy: Local
  ports:
    - name: pg
      protocol: TCP
      port: 5432
      targetPort: 5432
      nodePort: 32090
  selector:
    app.kubernetes.io/name: traefik
    app.kubernetes.io/instance: traefik-traefik
EOF

cat <<EOF | kubectl apply -f -
---
apiVersion: v1
kind: Service
metadata:
  name: traefik-web
  namespace: traefik
spec:
  type: NodePort
  externalTrafficPolicy: Local
  ports:
    - name: web
      protocol: TCP
      port: 80
      targetPort: web
      nodePort: 32091
  selector:
    app.kubernetes.io/name: traefik
    app.kubernetes.io/instance: traefik-traefik
EOF

kubectl apply -f https://raw.githubusercontent.com/kubernetes-csi/external-snapshotter/release-6.3/client/config/crd/snapshot.storage.k8s.io_volumesnapshotclasses.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes-csi/external-snapshotter/release-6.3/client/config/crd/snapshot.storage.k8s.io_volumesnapshotcontents.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes-csi/external-snapshotter/release-6.3/client/config/crd/snapshot.storage.k8s.io_volumesnapshots.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes-csi/external-snapshotter/v6.3.3/deploy/kubernetes/snapshot-controller/rbac-snapshot-controller.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes-csi/external-snapshotter/v6.3.3/deploy/kubernetes/snapshot-controller/setup-snapshot-controller.yaml

bash /home/makeitokay/csi-driver-host-path/deploy/kubernetes-latest/deploy.sh

cat <<EOF | kubectl apply -f -
---
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: csi-hostpath-sc
provisioner: hostpath.csi.k8s.io
reclaimPolicy: Delete
volumeBindingMode: Immediate
allowVolumeExpansion: true
EOF

HELM_CHART_VERSION="69.8.2"

helm repo add prometheus-community https://prometheus-community.github.io/helm-charts     
helm repo update

helm install kube-prom-stack prometheus-community/kube-prometheus-stack --version "${HELM_CHART_VERSION}" \
  --namespace monitoring \
  --create-namespace \
  -f "/Users/vasilyev.a/RiderProjects/pgaas/k8s-prom-stack-values.yaml"

cat <<EOF | kubectl apply -f -
---
apiVersion: traefik.io/v1alpha1
kind: IngressRoute
metadata:
  name: grafana-route
  namespace: monitoring
spec:
  entryPoints:
    - web
  routes:
    - match: Host(`grafana.pgaas.ru`)
      services:
      - name: kube-prom-stack-grafana
        port: 80
EOF

helm repo add crossplane-stable https://charts.crossplane.io/stable                                  
helm repo update

helm install crossplane --namespace crossplane-system crossplane-stable/crossplane --create-namespace
