#!/system/bin/bash

resource="https://github.com/qimgss/QShell"
branch="main"
rawfile="${resource}/raw/refs/${branch}"
version=""
versionType="test"
repo='qimgss/QShell'
profile="${rawfile}/profiles/"

red='\033[1;31m'
yel='\033[1;33m'
org='\033[38;2;255;165;0m'
ble='\033[0;94m'
ncr='\033[0m'

datafolder="/data/QShell"
channelfile=$(cat $datafolder/update/channel)
updateprofile="${datafolder}/version.yml"
qsbox="${datafolder}/qsbox"

version=""

exec 2> >(tee -a ${datafolder}/debug.log >&2)
set -x


prt(){
echo -e "$1"
}

setclr(){
prt "$1"
}

unsetclr(){
prt "${ncr}"
}

if [ ! -d ${datafolder} ]; then
    mkdir ${datafolder}
fi

main(){
clear
prt ""
setclr "${yel}"
prt "=============================="
prt "             QShell           "
prt "=============================="
unsetclr
prt "1.隐藏环境"
prt "2.解决单独的隐藏项"
prt "3.脚本设置"
prt "4.退出脚本"
prt "请输入选项(1~4)："
read mainipt
case ${mainipt} in
    1) hideenv ;;
    2) aloneenv ;;
    3) settings ;;
    4) exitsct ;;
esac
}

hideenv(){
[ ! -d ${datafolder}/modules ] && mkdir ${datafolder}/modules
modulefolder=${datafolder}/modules
download "${profiles}/modules.yml" "${datafolder}"
cd ${modulefolder}
yq '.general | to_entries[] | "\(.key) \(.value.url)"' ${profiles}/modules.yml | while read name url; do curl -L --progress-bar -k -o "$name.zip" "$url"; done
modulepaths=(("${modulefolder}"/*.zip))
install ${modulepaths}
rm -rf $modulepaths
}

aloneenv(){}

checkshell(){
command -v su2 >/dev/null 2>&1 && superuser_cmd=$(su2) || superuser_cmd=$(su) # MT管理器终端扩展包必须使用su2
if [ -z $BASH_VERSION ]; then
    prt "当前不是bash环境，正在通过qsbox进入bash环境"
    sleep 1
    $qsbox bash $0
else
    prt "当前为bash环境"
fi
}

settings(){
if [ $channelfile = "release" ] || [ ! -f $channelfile ] || [ -z $channelfile ]; then
    updatecnl="release"
    cnltxt="稳定"
elif [ $channelfile = "test" ]; then
    updatecnl="test"
    cnltxt="测试"
fi
clear
setclr "${yel}"
prt "=============================="
prt "             设置             "
prt "=============================="
unsetclr
prt "1.检查更新"
prt "2.更换更新通道 (当前：${cnltxt})"
prt "请输入选项(1~2)："
read setipt
case ${setipt} in
    1) checkudt ;;
    2) switchudt ;;
esac
}

exitsct(){
prt "退出脚本" >> /dev/null
set +x
exit 0
}

install(){
local modulefile=$1
local version=$(su -v)
if echo "${version}" | grep -qi magisk; then
    magisk --install-module ${modulefile}
elif echo "${version}" | grep -qi apatch; then
    apd module install ${modulefile}
elif echo "${version}" | grep -qi kernelsu; then
    ksud module install ${modulefile}
fi
}

download(){
local URL=$1
local OUTFILE=$2
curl -L --progress-bar -k -o ${OUTFILE} ${URL}
}

checkudt(){
rel_qshell=($qsbox yq '.releases.android.qshell' ${updateprofile})
rel_qsbox=($qsbox yq '.releases.android.qsbox' ${updateprofile})
test_qshell=($qsbox yq '.test.android.qshell' ${updateprofile})
test_qsbox=($qsbox yq '.test.android.qsbox' ${updateprofile})
download "${rawfile}/files/version.yml" "${updateprofile}"
if [ $channelfile = "release" ]; then
    qshell_cmd=${rel_qshell}
    qsbox_cmd=${rel_qsbox}
else
    qshell_cmd=${test_qshell}
    qsbox_cmd=${test_qsbox}
fi

if [[ ${version} -lt ${qshell_cmd} ]]; then
    if [ $channelfile = "release" ]; then
        update release script
    else
        update test script
    fi
fi


if [[ ${version} -lt ${qsbox_cmd} ]]; then
    if [ $channelfile = "release" ]; then
        update release binary
    else
        update test binary
    fi
fi
}

update(){
local method=$1
local target=$2
[ "${method}" = "release" ] && local link=${resource}/releases/latest/download || local link=${rawfile}
if [ "${target}" = "script" ]; then
    download "${link}/QShell.sh" "$0"
elif [ "${target}" = "binary" ]; then
    if [ "${method}" = "release" ]; then
        download "${link}/QSBox" "${qsbox}"
    elif [ "${method}" = "test" ]; then
        ${qsbox} githubdl actions $repo latest qsbox -o
    fi
fi

}

switchudt(){
if [ $channelfile = "release" ]; then
    rm -rf $channelfile
    prt "test" >> $channelfile
    prt "已切换更新源为test"
else
    rm -rf $channelfile
    prt "release" >> $channelfile
    prt "已切换更新源为release"
fi
}

update release binary
checkshell
main