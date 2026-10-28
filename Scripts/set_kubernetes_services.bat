@echo off
set namespace=%1
set container-registry=%2
set image=%3

if "%namespace%"=="" (
    echo Usage: set_kubernetes_services.bat [namespace] [container-registry] [image]
    exit /b 1
)
if "%container-registry%"=="" (
    echo Usage: set_kubernetes_services.bat [namespace] [container-registry] [image]
    exit /b 1
)
if "%image%"=="" (
    echo Usage: set_kubernetes_services.bat [namespace] [container-registry] [image]
    exit /b 1
)

kubectl set -n %namespace% image -n %namespace% deployment account-deploy account=%container-registry%/link-account:%image%
kubectl set -n %namespace% image -n %namespace% deployment admin-deploy admin=%container-registry%/link-admin:%image%
kubectl set -n %namespace% image -n %namespace% deployment admin-ui-deploy admin-ui=%container-registry%/link-admin-ui:%image%
kubectl set -n %namespace% image -n %namespace% deployment audit-deploy audit=%container-registry%/link-audit:%image%
kubectl set -n %namespace% image -n %namespace% deployment census-deploy census=%container-registry%/link-census:%image%
kubectl set -n %namespace% image -n %namespace% deployment data-acquisition-deploy data=%container-registry%/link-dataacquisition:%image%
kubectl set -n %namespace% image -n %namespace% deployment data-acquisition-worker-deploy data-worker=%container-registry%/link-dataacquisition-worker:%image%
kubectl set -n %namespace% image -n %namespace% deployment measure-deploy measure=%container-registry%/link-measureeval:%image%
kubectl set -n %namespace% image -n %namespace% deployment normalization-deploy normalization=%container-registry%/link-normalization:%image%
kubectl set -n %namespace% image -n %namespace% deployment query-dispatch-deploy query-dispatch=%container-registry%/link-querydispatch:%image%
kubectl set -n %namespace% image -n %namespace% deployment report-deploy report=%container-registry%/link-report:%image%
kubectl set -n %namespace% image -n %namespace% deployment submission-deploy submission=%container-registry%/link-submission:%image%
kubectl set -n %namespace% image -n %namespace% deployment tenant-deploy tenant=%container-registry%/link-tenant:%image%
kubectl set -n %namespace% image -n %namespace% deployment terminology-deploy terminology=%container-registry%/link-terminology:%image%
kubectl set -n %namespace% image -n %namespace% deployment validation-deploy validation=%container-registry%/link-validation:%image%
kubectl set -n %namespace% image -n %namespace% deployment mock-dmrp-deploy mock-dmrp=%container-registry%/link-mock-dmrp:%image%