docker build . -t pgaas-backend
kind load docker-image pgaas-backend --name pgaas
kubectl rollout restart deployment/pgaas-backend -n infrastructure