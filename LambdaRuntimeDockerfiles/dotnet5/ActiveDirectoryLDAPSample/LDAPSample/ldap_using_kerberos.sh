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
        --keytab-file-s3-url)
            shift;
            KEYTAB_FILE_S3_URL="$1"
            continue
            ;;
        --filter)
            shift;
            FILTER="$1"
            continue
            ;;
    esac
    shift
done

export KRB5CCNAME=/tmp/krb5cc
export PATH=./:$PATH
export LD_LIBRARY_PATH=./:$LD_LIBRARY_PATH
REALM=$(echo "$DIRECTORY_NAME" | tr [a-z] [A-Z])

# Add endpoint as per https://aws.amazon.com/blogs/aws/new-vpc-endpoint-for-amazon-s3/
KEYTAB_FILENAME=$(basename ${KEYTAB_FILE_S3_URL})
for i in seq 3; do
    aws s3 cp ${KEYTAB_FILE_S3_URL} /tmp/${KEYTAB_FILENAME} 2>&1 > /dev/null
    STATUS=$?
    if [ $? -eq 0 ]; then
       break
    fi
    sleep 5
done
if [ $STATUS -ne 0 ]; then
   echo "**ERROR** AWS S3 cp failed"
   exit 1
fi

for i in seq 5; do
  RETURN_VALUE=$(kinit ${USER_NAME}@${REALM} -kt /tmp/${KEYTAB_FILENAME} 2>&1 | tr -d '\n')
  if ! echo ${RETURN_VALUE} | grep -i error; then
     /var/task/openldap/clients/tools/ldapsearch -H ldap://${DIRECTORY_NAME} -b ${BASE_DN} -Y GSSAPI ${FILTER}
     kdestroy -Aq
     exit 0
  fi
  sleep 20
done

exit 1
