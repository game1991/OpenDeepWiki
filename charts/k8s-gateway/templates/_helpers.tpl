{{/*
k8s-gateway chart helpers
*/}}
{{- define "k8s-gateway.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "k8s-gateway.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name (include "k8s-gateway.name" .) | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}

{{- define "k8s-gateway.labels" -}}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" }}
{{ include "k8s-gateway.selectorLabels" . }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{- define "k8s-gateway.selectorLabels" -}}
app.kubernetes.io/name: {{ include "k8s-gateway.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}