FROM node:24-alpine AS build
WORKDIR /app
COPY src/MTL.Web/package.json src/MTL.Web/package-lock.json ./
RUN npm ci
COPY src/MTL.Web/ ./
RUN npm run build

FROM nginx:1.29-alpine AS runtime
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80

