namespace ArtNet.Runtime
{
    public enum ArtNetOpCode
    {
        /// <summary>
        /// 0x2000 This is an ArtPoll packet, no other data is contained in this UDP packet
        /// </summary>
        OpPoll = 0x2000,

        /// <summary>
        /// 0x2100 This is an ArtPollReply Packet. It contains device
        /// </summary>
        OpPollReply = 0x2100,

        /// <summary>
        /// 0x2300 Diagnostics and data logging packet.
        /// </summary>
        OpDiagData = 0x2300,

        /// <summary>
        /// 0x2400 Used to send text based parameter commands.
        /// </summary>
        OpCommand = 0x2400,

        /// <summary>
        /// OpDmx 0x5000 This is an ArtDmx data packet. It contains zero start
        /// code, DMX512 information for a single Universe.
        /// </summary>
        OpDmx = 0x5000,

        /// <summary>
        /// 0x5100 This is an ArtNzs data packet. It contains non-zero
        /// start, code (except RDM) DMX512 information for a
        /// single Universe.
        /// </summary>
        OpNzs = 0x5100,

        /// <summary>
        /// 0x5200 This is an ArtSync data packet. It is used to force
        /// synchronous, transfer of ArtDmx packets to a node’s
        /// output.
        /// </summary>
        OpSync = 0x5200,

        /// <summary>
        /// 0x6000 This is an ArtAddress packet. It contains remote
        /// programming, information for a Node.
        /// </summary>
        OpAddress = 0x6000,

        /// <summary>
        /// 0x7000 This is an ArtInput packet. It contains enable –
        /// disable, data for DMX inputs.
        /// </summary>
        OpInput = 0x7000,

        /// <summary>
        /// 0x8000 This is an ArtTodRequest packet. It is used to request
        /// a, Table of Devices (ToD) for RDM discovery.
        /// </summary>
        OpTodRequest = 0x8000,

        /// <summary>
        /// 0x8100 This is an ArtTodData packet. It is used to send a
        /// Table, of Devices (ToD) for RDM discovery.
        /// </summary>
        OpTodData = 0x8100,

        /// <summary>
        /// 0x8200 This is an ArtTodControl packet. It is used to send
        /// discovery control messages.
        /// </summary>
        OpTodControl = 0x8200,

        /// <summary>
        /// 0x8300 This is an ArtRdm packet. It is used to send all non
        /// discovery, RDM messages.
        /// </summary>
        OpRdm = 0x8300,

        /// <summary>
        /// 0x8400 This is an ArtRdmSub packet. It is used to send
        /// compressed, RDM Sub-Device data.
        /// </summary>
        OpRdmSub = 0x8400,

        /// <summary>
        /// 0xa010 This is an ArtVideoSetup packet. It contains video
        /// screen, setup information for nodes that implement
        /// the, extended video features.
        /// </summary>
        OpVideoSetup = 0xa010,

        /// <summary>
        /// 0xa020 This is an ArtVideoPalette packet. It contains colour
        /// palette, setup information for nodes that implement
        /// the, extended video features.
        /// </summary>
        OpVideoPalette = 0xa020,

        /// <summary>
        /// 0xa040 This is an ArtVideoData packet. It contains display
        /// data, for nodes that implement the extended video
        /// features.
        /// </summary>
        OpVideoData = 0xa040,

        /// <summary>
        /// 0xf000 This packet is deprecated.
        /// </summary>
        OpMacMaster = 0xf000,

        /// <summary>
        /// 0xf100 This packet is deprecated.
        /// </summary>
        OpMacSlave = 0xf100,

        /// <summary>
        /// 0xf200 This is an ArtFirmwareMaster packet. It is used to
        /// upload, new firmware or firmware extensions to the
        /// Node.
        /// </summary>
        OpFirmwareMaster = 0xf200,

        /// <summary>
        /// 0xf300 This is an ArtFirmwareReply packet. It is returned by
        /// the, node to acknowledge receipt of an ArtFirmWareMaster packet or ArtFileTnMaster
        /// packet.
        /// </summary>
        OpFirmwareReply = 0xf300,

        /// <summary>
        /// 0xf400 Uploads user file to node.
        /// </summary>
        OpFileTnMaster = 0xf400,

        /// <summary>
        /// 0xf500 Downloads user file from node.
        /// </summary>
        OpFileFnMaster = 0xf500,

        /// <summary>
        /// 0xf600 Server to Node acknowledge for download packets.
        /// </summary>
        OpFileFnReply = 0xf600,

        /// <summary>
        /// 0xf800 This is an ArtIpProg packet. It is used to reprogramme the IP address and Mask of the Node.
        /// </summary>
        OpIpProg = 0xf800,

        /// <summary>
        /// 0xf900 This is an ArtIpProgReply packet. It is returned by the node, to acknowledge receipt of an ArtIpProg packet.
        /// </summary>
        OpIpProgReply = 0xf900,

        /// <summary>
        /// 0x9000 This is an ArtMedia packet. It is Unicast by a Media Server, and acted upon by a Controller.
        /// </summary>
        OpMedia = 0x9000,

        /// <summary>
        /// 0x9100 This is an ArtMediaPatch packet. It is Unicast by a Controller, and acted upon by a Media Server.
        /// </summary>
        OpMediaPatch = 0x9100,

        /// <summary>
        /// 0x9200 This is an ArtMediaControl packet. It is Unicast by a Controller, and acted upon by a Media Server.
        /// </summary>
        OpMediaControl = 0x9200,

        /// <summary>
        /// 0x9300 This is an ArtMediaControlReply packet. It is Unicast by, a Media Server and acted upon by a Controller.
        /// </summary>
        OpMediaContrlReply = 0x9300,

        /// <summary>
        /// 0x9700 This is an ArtTimeCode packet. It is used to transport time, code over the network.
        /// </summary>
        OpTimeCode = 0x9700,

        /// <summary>
        /// 0x9800 Used to synchronise real time date and clock
        /// </summary>
        OpTimeSync = 0x9800,

        /// <summary>
        /// 0x9900 Used to send trigger macros
        /// </summary>
        OpTrigger = 0x9900,

        /// <summary>
        /// 0x9a00 Requests a node's file list
        /// </summary>
        OpDirectory = 0x9a00,

        /// <summary>
        /// 0x9b00 Replies to OpDirectory with file list
        /// </summary>
        OpDirectoryReply = 0x9b00,
    }
}
