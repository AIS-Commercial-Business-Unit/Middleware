{{/* Generate a deterministic fullname; release-name + chart-name. */}}
{{- define "kafdrop.fullname" -}}
{{- printf "%s-kafdrop" .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "kafdrop.labels" -}}
app.kubernetes.io/name: kafdrop
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version }}
{{- end -}}

{{- define "kafdrop.selectorLabels" -}}
app.kubernetes.io/name: kafdrop
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}
