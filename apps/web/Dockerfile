# syntax=docker/dockerfile:1
FROM node:22-bookworm AS build
WORKDIR /app
ENV CI=true
RUN corepack enable

COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .
RUN pnpm build

FROM nginx:1.27-alpine
# default.conf.template обрабатывается штатным envsubst-энтрипоинтом nginx
# (templates → conf.d). NGINX_ENVSUBST_FILTER ограничивает подстановку нашим
# префиксом, чтобы $uri/$host/$http_upgrade не были затёрты.
ENV THRONE_API_UPSTREAM=api:5008
ENV NGINX_ENVSUBST_FILTER=THRONE_
COPY docker/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
