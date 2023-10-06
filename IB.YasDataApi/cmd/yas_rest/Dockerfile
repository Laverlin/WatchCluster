
## Build
FROM golang:1.19-alpine AS build
WORKDIR /src

COPY ../../ ./IB.YasDataApi/

RUN cd IB.YasDataApi && go mod download
RUN cd ./IB.YasDataApi/cmd/yas_rest && go build -o /release

## Deploy
FROM alpine:3.17 as final
WORKDIR /app

COPY --from=build /release .
EXPOSE 8989

ENTRYPOINT ["/app/release"]