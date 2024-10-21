FROM node:18-alpine AS build

RUN mkdir /build

WORKDIR /build

COPY package.json /build/
COPY package-lock.json /build/

RUN npm ci

COPY . .
RUN npm install -g @angular/cli
RUN ng build

FROM nginx:1.27.2-alpine
COPY nginx.conf /etc/nginx/nginx.conf
COPY --from=build /build/dist/demo-app/. /usr/share/nginx/html
EXPOSE 80
