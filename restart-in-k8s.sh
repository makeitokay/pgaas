docker build . -t localhost:5001/pgaas-backend
docker push localhost:5001/pgaas-backend:latest

DEPLOYMENT_NAME="pgaas-backend"
NAMESPACE="infrastructure"

if kubectl get deployment "$DEPLOYMENT_NAME" -n "$NAMESPACE" > /dev/null 2>&1; then
    kubectl rollout restart deployment/"$DEPLOYMENT_NAME" -n "$NAMESPACE"
else
    helm install infrastructure charts/infrastructure --namespace $NAMESPACE --create-namespace
fi
