FROM ubuntu:bionic
 
RUN apt-get autoclean && apt-get autoremove && apt-get update && \
    apt-get -qqy install --no-install-recommends build-essential \
    automake ncurses-dev libcurl4-openssl-dev libssl-dev libgtest-dev \
    make autoconf automake libtool git apt-utils pkg-config libc6-dev \
    libcurl3-dev libudev-dev m4 g++-multilib unzip git python zlib1g-dev \
    wget ca-certificates pwgen bsdmainutils

RUN apt-get install unzip

WORKDIR /temp
RUN wget -O snowgem.zip https://github.com/Snowgem/Snowgem/releases/download/3000453-20190603/snowgem-ubuntu18.04-3000453-20190603.zip

RUN chmod +rwx snowgem.zip
RUN unzip snowgem.zip -d /node

WORKDIR /node
ADD https://raw.githubusercontent.com/Snowgem/masternode-upgrade/master/fetch-params.sh ./fetch-params.sh
RUN chmod +rwx fetch-params.sh
RUN ./fetch-params.sh

COPY snowgem.conf /root/.snowgem/snowgem.conf

ENTRYPOINT [ "./snowgemd" ]