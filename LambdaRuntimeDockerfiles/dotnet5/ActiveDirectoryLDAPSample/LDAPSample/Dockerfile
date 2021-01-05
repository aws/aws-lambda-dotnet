ARG AWS_LAMBDA_VERSION=local

FROM aws-lambda-dotnet:$AWS_LAMBDA_VERSION AS base
RUN yum install -y krb5-workstation krb5-libs krb5-auth-dialog git gcc lib cyrus-sasl cyrus-sasl-gssapi make cyrus-sasl-devel cyrus-sasl-lib awscli

# Compiling for now, since there seems to be an issue with the yum installed sasl and ldap
RUN git clone https://github.com/openldap/openldap.git && cd openldap && ./configure --with-sasl && make install && yum erase git gcc --assumeyes

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim as build
COPY . .
RUN dotnet build "LDAPSample/LDAPSample.csproj" -o /app/build

FROM build AS publish
RUN dotnet publish "LDAPSample/LDAPSample.csproj" -c Release -o /app/publish
COPY LDAPSample/ldap_using_kerberos.sh /app/publish
FROM base AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
