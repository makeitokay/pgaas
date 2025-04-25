GRAFANA_TOKEN="<token_here>"

cat <<EOF | kubectl apply -f -
---
apiVersion: pkg.crossplane.io/v1
kind: Provider
metadata:
  name: provider-grafana
  namespace: crossplane-system
spec:
  package: xpkg.upbound.io/grafana/provider-grafana:v0.9.0
---
apiVersion: grafana.crossplane.io/v1beta1
kind: ProviderConfig
metadata:
  name: grafana-providerconfig
  namespace: crossplane-system
spec:
  credentials:
    source: Secret
    secretRef:
      name: grafana-cloud-creds
      namespace: crossplane-system
      key: credentials
---
apiVersion: v1
kind: Secret
metadata:
  name: grafana-cloud-creds
  namespace: crossplane-system
stringData:
  credentials: |
    { 
      "url": "http://kube-prom-stack-grafana.monitoring.svc.cluster.local", 
      "auth": "$GRAFANA_TOKEN" 
    }
EOF