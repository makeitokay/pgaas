kind create cluster --name pgaas --config ./kind.yaml
kind export kubeconfig

kind load docker-image pgaas-backend --name pgaas

kubectl apply --server-side -f \
  https://raw.githubusercontent.com/cloudnative-pg/cloudnative-pg/release-1.25/releases/cnpg-1.25.0.yaml
