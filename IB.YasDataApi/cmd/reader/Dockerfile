
## Build
FROM golang:1.19-alpine AS build
WORKDIR /src

COPY ../../go.mod ./
COPY ../../go.sum ./
RUN go mod download

#COPY ../../abstract/*.go ./abstract/
#COPY ../../abstract/*.go ./abstract/
COPY ../../*.go ./

RUN ls -la
RUN cd ./IB.YasDataApi/cmd/reader && go build -v -o /release

## Deploy
FROM alpine:3.17 as final

WORKDIR /app

COPY --from=build /release .

EXPOSE 8989

USER nonroot:nonroot

ENTRYPOINT ["/app"]