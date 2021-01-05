#!/bin/sh

for i in "$@"; do
    case "$i" in
        --directory-name)
            shift
            DIRECTORY_NAME="$1"
            continue
            ;;
        --user-name)
            shift;
            USER_NAME="$1"
            continue
            ;;
        --base-dn)
            shift;
            BASE_DN="$1"
            continue
            ;;
        --vpc-endpoint-secretsmanager-url)
            shift;
            VPC_ENDPOINT_SECRETSMANAGER="$1"
            continue
            ;;
        --filter)
            shift;
            FILTER="$1"
            continue
            ;;
        --region)
            shift;
            REGION="$1"
            continue
            ;;
        --secretsmanager-keytab-secret-id)
            shift;
            KEYTAB_SECRET_ID="$1"
            continue
            ;;
    esac
    shift
done

export KRB5CCNAME=/tmp/krb5cc
export PATH=./:$PATH
export LD_LIBRARY_PATH=./:$LD_LIBRARY_PATH
REALM=$(echo "$DIRECTORY_NAME" | tr [a-z] [A-Z])

aws secretsmanager get-secret-value --secret-id ${KEYTAB_SECRET_ID} --endpoint-url ${VPC_ENDPOINT_SECRETSMANAGER} --region ${REGION} | jq -r .SecretString > /tmp/base64.keytab
base64 -d /tmp/base64.keytab > /tmp/keytab

for i in seq 5; do
  RETURN_VALUE=$(kinit ${USER_NAME}@${REALM} -kt /tmp/keytab 2>&1 | tr -d '\n')
  if ! echo ${RETURN_VALUE} | grep -i error; then
     /var/task/openldap/clients/tools/ldapsearch -H ldap://${DIRECTORY_NAME} -b ${BASE_DN} -Y GSSAPI ${FILTER}
     kdestroy -Aq
     exit 0
  fi
  sleep 20
done

exit 1
