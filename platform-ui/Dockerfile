FROM node:22-alpine AS deps
WORKDIR /app
COPY package*.json ./
RUN npm ci

FROM node:22-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ENV NEXT_TELEMETRY_DISABLED=1

# NEXT_PUBLIC_* vars are inlined at build time for client components.
# Pass these as --build-arg in CI for AKS builds; defaults match local dev.
ARG NEXT_PUBLIC_KAFDROP_URL=http://localhost:9000
ARG NEXT_PUBLIC_GRAFANA_URL=http://localhost:3001
ARG NEXT_PUBLIC_MONGO_URL=http://localhost:27017
ENV NEXT_PUBLIC_KAFDROP_URL=$NEXT_PUBLIC_KAFDROP_URL
ENV NEXT_PUBLIC_GRAFANA_URL=$NEXT_PUBLIC_GRAFANA_URL
ENV NEXT_PUBLIC_MONGO_URL=$NEXT_PUBLIC_MONGO_URL

RUN npm run build

FROM node:22-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
COPY --from=builder /app/public ./public
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
EXPOSE 3000
ENV PORT=3000
CMD ["node", "server.js"]
