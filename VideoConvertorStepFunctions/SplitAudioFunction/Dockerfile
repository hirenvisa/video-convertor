# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SplitAudioFunction.csproj ./
RUN dotnet restore SplitAudioFunction.csproj

COPY . .
RUN dotnet publish SplitAudioFunction.csproj -c Release -o /out/publish

# Stage 2: Runtime
FROM public.ecr.aws/lambda/dotnet:8

# Install ffmpeg and libgdiplus
RUN yum install -y epel-release \
    && yum install -y ffmpeg libgdiplus \
    && yum clean all

WORKDIR /var/task
COPY --from=build /out/publish .

# Specify the Lambda function handler
CMD [ "SplitAudioFunction::SplitAudioFunction.Function::FunctionHandler" ]
