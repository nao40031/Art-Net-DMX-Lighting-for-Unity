namespace ArtNet.Runtime
{
    public partial class ArtNetChannels
    {
        public bool CopyTo(byte[] dst)
        {
            if (dst == null || dst.Length < 512) return false;
            bool changed = false;
            int v;
            v = DmxValueUtils.ClampByte(Ch1);
            if (dst[0] != v) { dst[0] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch2);
            if (dst[1] != v) { dst[1] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch3);
            if (dst[2] != v) { dst[2] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch4);
            if (dst[3] != v) { dst[3] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch5);
            if (dst[4] != v) { dst[4] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch6);
            if (dst[5] != v) { dst[5] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch7);
            if (dst[6] != v) { dst[6] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch8);
            if (dst[7] != v) { dst[7] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch9);
            if (dst[8] != v) { dst[8] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch10);
            if (dst[9] != v) { dst[9] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch11);
            if (dst[10] != v) { dst[10] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch12);
            if (dst[11] != v) { dst[11] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch13);
            if (dst[12] != v) { dst[12] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch14);
            if (dst[13] != v) { dst[13] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch15);
            if (dst[14] != v) { dst[14] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch16);
            if (dst[15] != v) { dst[15] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch17);
            if (dst[16] != v) { dst[16] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch18);
            if (dst[17] != v) { dst[17] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch19);
            if (dst[18] != v) { dst[18] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch20);
            if (dst[19] != v) { dst[19] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch21);
            if (dst[20] != v) { dst[20] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch22);
            if (dst[21] != v) { dst[21] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch23);
            if (dst[22] != v) { dst[22] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch24);
            if (dst[23] != v) { dst[23] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch25);
            if (dst[24] != v) { dst[24] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch26);
            if (dst[25] != v) { dst[25] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch27);
            if (dst[26] != v) { dst[26] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch28);
            if (dst[27] != v) { dst[27] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch29);
            if (dst[28] != v) { dst[28] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch30);
            if (dst[29] != v) { dst[29] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch31);
            if (dst[30] != v) { dst[30] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch32);
            if (dst[31] != v) { dst[31] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch33);
            if (dst[32] != v) { dst[32] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch34);
            if (dst[33] != v) { dst[33] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch35);
            if (dst[34] != v) { dst[34] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch36);
            if (dst[35] != v) { dst[35] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch37);
            if (dst[36] != v) { dst[36] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch38);
            if (dst[37] != v) { dst[37] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch39);
            if (dst[38] != v) { dst[38] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch40);
            if (dst[39] != v) { dst[39] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch41);
            if (dst[40] != v) { dst[40] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch42);
            if (dst[41] != v) { dst[41] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch43);
            if (dst[42] != v) { dst[42] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch44);
            if (dst[43] != v) { dst[43] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch45);
            if (dst[44] != v) { dst[44] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch46);
            if (dst[45] != v) { dst[45] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch47);
            if (dst[46] != v) { dst[46] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch48);
            if (dst[47] != v) { dst[47] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch49);
            if (dst[48] != v) { dst[48] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch50);
            if (dst[49] != v) { dst[49] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch51);
            if (dst[50] != v) { dst[50] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch52);
            if (dst[51] != v) { dst[51] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch53);
            if (dst[52] != v) { dst[52] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch54);
            if (dst[53] != v) { dst[53] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch55);
            if (dst[54] != v) { dst[54] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch56);
            if (dst[55] != v) { dst[55] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch57);
            if (dst[56] != v) { dst[56] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch58);
            if (dst[57] != v) { dst[57] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch59);
            if (dst[58] != v) { dst[58] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch60);
            if (dst[59] != v) { dst[59] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch61);
            if (dst[60] != v) { dst[60] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch62);
            if (dst[61] != v) { dst[61] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch63);
            if (dst[62] != v) { dst[62] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch64);
            if (dst[63] != v) { dst[63] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch65);
            if (dst[64] != v) { dst[64] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch66);
            if (dst[65] != v) { dst[65] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch67);
            if (dst[66] != v) { dst[66] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch68);
            if (dst[67] != v) { dst[67] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch69);
            if (dst[68] != v) { dst[68] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch70);
            if (dst[69] != v) { dst[69] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch71);
            if (dst[70] != v) { dst[70] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch72);
            if (dst[71] != v) { dst[71] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch73);
            if (dst[72] != v) { dst[72] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch74);
            if (dst[73] != v) { dst[73] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch75);
            if (dst[74] != v) { dst[74] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch76);
            if (dst[75] != v) { dst[75] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch77);
            if (dst[76] != v) { dst[76] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch78);
            if (dst[77] != v) { dst[77] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch79);
            if (dst[78] != v) { dst[78] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch80);
            if (dst[79] != v) { dst[79] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch81);
            if (dst[80] != v) { dst[80] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch82);
            if (dst[81] != v) { dst[81] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch83);
            if (dst[82] != v) { dst[82] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch84);
            if (dst[83] != v) { dst[83] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch85);
            if (dst[84] != v) { dst[84] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch86);
            if (dst[85] != v) { dst[85] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch87);
            if (dst[86] != v) { dst[86] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch88);
            if (dst[87] != v) { dst[87] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch89);
            if (dst[88] != v) { dst[88] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch90);
            if (dst[89] != v) { dst[89] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch91);
            if (dst[90] != v) { dst[90] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch92);
            if (dst[91] != v) { dst[91] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch93);
            if (dst[92] != v) { dst[92] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch94);
            if (dst[93] != v) { dst[93] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch95);
            if (dst[94] != v) { dst[94] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch96);
            if (dst[95] != v) { dst[95] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch97);
            if (dst[96] != v) { dst[96] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch98);
            if (dst[97] != v) { dst[97] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch99);
            if (dst[98] != v) { dst[98] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch100);
            if (dst[99] != v) { dst[99] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch101);
            if (dst[100] != v) { dst[100] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch102);
            if (dst[101] != v) { dst[101] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch103);
            if (dst[102] != v) { dst[102] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch104);
            if (dst[103] != v) { dst[103] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch105);
            if (dst[104] != v) { dst[104] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch106);
            if (dst[105] != v) { dst[105] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch107);
            if (dst[106] != v) { dst[106] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch108);
            if (dst[107] != v) { dst[107] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch109);
            if (dst[108] != v) { dst[108] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch110);
            if (dst[109] != v) { dst[109] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch111);
            if (dst[110] != v) { dst[110] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch112);
            if (dst[111] != v) { dst[111] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch113);
            if (dst[112] != v) { dst[112] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch114);
            if (dst[113] != v) { dst[113] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch115);
            if (dst[114] != v) { dst[114] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch116);
            if (dst[115] != v) { dst[115] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch117);
            if (dst[116] != v) { dst[116] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch118);
            if (dst[117] != v) { dst[117] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch119);
            if (dst[118] != v) { dst[118] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch120);
            if (dst[119] != v) { dst[119] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch121);
            if (dst[120] != v) { dst[120] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch122);
            if (dst[121] != v) { dst[121] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch123);
            if (dst[122] != v) { dst[122] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch124);
            if (dst[123] != v) { dst[123] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch125);
            if (dst[124] != v) { dst[124] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch126);
            if (dst[125] != v) { dst[125] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch127);
            if (dst[126] != v) { dst[126] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch128);
            if (dst[127] != v) { dst[127] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch129);
            if (dst[128] != v) { dst[128] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch130);
            if (dst[129] != v) { dst[129] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch131);
            if (dst[130] != v) { dst[130] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch132);
            if (dst[131] != v) { dst[131] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch133);
            if (dst[132] != v) { dst[132] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch134);
            if (dst[133] != v) { dst[133] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch135);
            if (dst[134] != v) { dst[134] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch136);
            if (dst[135] != v) { dst[135] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch137);
            if (dst[136] != v) { dst[136] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch138);
            if (dst[137] != v) { dst[137] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch139);
            if (dst[138] != v) { dst[138] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch140);
            if (dst[139] != v) { dst[139] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch141);
            if (dst[140] != v) { dst[140] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch142);
            if (dst[141] != v) { dst[141] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch143);
            if (dst[142] != v) { dst[142] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch144);
            if (dst[143] != v) { dst[143] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch145);
            if (dst[144] != v) { dst[144] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch146);
            if (dst[145] != v) { dst[145] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch147);
            if (dst[146] != v) { dst[146] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch148);
            if (dst[147] != v) { dst[147] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch149);
            if (dst[148] != v) { dst[148] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch150);
            if (dst[149] != v) { dst[149] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch151);
            if (dst[150] != v) { dst[150] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch152);
            if (dst[151] != v) { dst[151] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch153);
            if (dst[152] != v) { dst[152] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch154);
            if (dst[153] != v) { dst[153] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch155);
            if (dst[154] != v) { dst[154] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch156);
            if (dst[155] != v) { dst[155] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch157);
            if (dst[156] != v) { dst[156] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch158);
            if (dst[157] != v) { dst[157] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch159);
            if (dst[158] != v) { dst[158] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch160);
            if (dst[159] != v) { dst[159] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch161);
            if (dst[160] != v) { dst[160] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch162);
            if (dst[161] != v) { dst[161] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch163);
            if (dst[162] != v) { dst[162] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch164);
            if (dst[163] != v) { dst[163] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch165);
            if (dst[164] != v) { dst[164] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch166);
            if (dst[165] != v) { dst[165] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch167);
            if (dst[166] != v) { dst[166] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch168);
            if (dst[167] != v) { dst[167] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch169);
            if (dst[168] != v) { dst[168] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch170);
            if (dst[169] != v) { dst[169] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch171);
            if (dst[170] != v) { dst[170] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch172);
            if (dst[171] != v) { dst[171] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch173);
            if (dst[172] != v) { dst[172] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch174);
            if (dst[173] != v) { dst[173] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch175);
            if (dst[174] != v) { dst[174] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch176);
            if (dst[175] != v) { dst[175] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch177);
            if (dst[176] != v) { dst[176] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch178);
            if (dst[177] != v) { dst[177] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch179);
            if (dst[178] != v) { dst[178] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch180);
            if (dst[179] != v) { dst[179] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch181);
            if (dst[180] != v) { dst[180] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch182);
            if (dst[181] != v) { dst[181] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch183);
            if (dst[182] != v) { dst[182] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch184);
            if (dst[183] != v) { dst[183] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch185);
            if (dst[184] != v) { dst[184] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch186);
            if (dst[185] != v) { dst[185] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch187);
            if (dst[186] != v) { dst[186] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch188);
            if (dst[187] != v) { dst[187] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch189);
            if (dst[188] != v) { dst[188] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch190);
            if (dst[189] != v) { dst[189] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch191);
            if (dst[190] != v) { dst[190] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch192);
            if (dst[191] != v) { dst[191] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch193);
            if (dst[192] != v) { dst[192] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch194);
            if (dst[193] != v) { dst[193] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch195);
            if (dst[194] != v) { dst[194] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch196);
            if (dst[195] != v) { dst[195] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch197);
            if (dst[196] != v) { dst[196] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch198);
            if (dst[197] != v) { dst[197] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch199);
            if (dst[198] != v) { dst[198] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch200);
            if (dst[199] != v) { dst[199] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch201);
            if (dst[200] != v) { dst[200] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch202);
            if (dst[201] != v) { dst[201] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch203);
            if (dst[202] != v) { dst[202] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch204);
            if (dst[203] != v) { dst[203] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch205);
            if (dst[204] != v) { dst[204] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch206);
            if (dst[205] != v) { dst[205] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch207);
            if (dst[206] != v) { dst[206] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch208);
            if (dst[207] != v) { dst[207] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch209);
            if (dst[208] != v) { dst[208] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch210);
            if (dst[209] != v) { dst[209] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch211);
            if (dst[210] != v) { dst[210] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch212);
            if (dst[211] != v) { dst[211] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch213);
            if (dst[212] != v) { dst[212] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch214);
            if (dst[213] != v) { dst[213] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch215);
            if (dst[214] != v) { dst[214] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch216);
            if (dst[215] != v) { dst[215] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch217);
            if (dst[216] != v) { dst[216] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch218);
            if (dst[217] != v) { dst[217] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch219);
            if (dst[218] != v) { dst[218] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch220);
            if (dst[219] != v) { dst[219] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch221);
            if (dst[220] != v) { dst[220] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch222);
            if (dst[221] != v) { dst[221] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch223);
            if (dst[222] != v) { dst[222] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch224);
            if (dst[223] != v) { dst[223] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch225);
            if (dst[224] != v) { dst[224] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch226);
            if (dst[225] != v) { dst[225] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch227);
            if (dst[226] != v) { dst[226] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch228);
            if (dst[227] != v) { dst[227] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch229);
            if (dst[228] != v) { dst[228] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch230);
            if (dst[229] != v) { dst[229] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch231);
            if (dst[230] != v) { dst[230] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch232);
            if (dst[231] != v) { dst[231] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch233);
            if (dst[232] != v) { dst[232] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch234);
            if (dst[233] != v) { dst[233] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch235);
            if (dst[234] != v) { dst[234] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch236);
            if (dst[235] != v) { dst[235] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch237);
            if (dst[236] != v) { dst[236] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch238);
            if (dst[237] != v) { dst[237] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch239);
            if (dst[238] != v) { dst[238] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch240);
            if (dst[239] != v) { dst[239] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch241);
            if (dst[240] != v) { dst[240] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch242);
            if (dst[241] != v) { dst[241] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch243);
            if (dst[242] != v) { dst[242] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch244);
            if (dst[243] != v) { dst[243] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch245);
            if (dst[244] != v) { dst[244] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch246);
            if (dst[245] != v) { dst[245] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch247);
            if (dst[246] != v) { dst[246] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch248);
            if (dst[247] != v) { dst[247] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch249);
            if (dst[248] != v) { dst[248] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch250);
            if (dst[249] != v) { dst[249] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch251);
            if (dst[250] != v) { dst[250] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch252);
            if (dst[251] != v) { dst[251] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch253);
            if (dst[252] != v) { dst[252] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch254);
            if (dst[253] != v) { dst[253] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch255);
            if (dst[254] != v) { dst[254] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch256);
            if (dst[255] != v) { dst[255] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch257);
            if (dst[256] != v) { dst[256] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch258);
            if (dst[257] != v) { dst[257] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch259);
            if (dst[258] != v) { dst[258] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch260);
            if (dst[259] != v) { dst[259] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch261);
            if (dst[260] != v) { dst[260] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch262);
            if (dst[261] != v) { dst[261] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch263);
            if (dst[262] != v) { dst[262] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch264);
            if (dst[263] != v) { dst[263] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch265);
            if (dst[264] != v) { dst[264] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch266);
            if (dst[265] != v) { dst[265] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch267);
            if (dst[266] != v) { dst[266] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch268);
            if (dst[267] != v) { dst[267] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch269);
            if (dst[268] != v) { dst[268] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch270);
            if (dst[269] != v) { dst[269] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch271);
            if (dst[270] != v) { dst[270] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch272);
            if (dst[271] != v) { dst[271] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch273);
            if (dst[272] != v) { dst[272] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch274);
            if (dst[273] != v) { dst[273] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch275);
            if (dst[274] != v) { dst[274] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch276);
            if (dst[275] != v) { dst[275] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch277);
            if (dst[276] != v) { dst[276] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch278);
            if (dst[277] != v) { dst[277] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch279);
            if (dst[278] != v) { dst[278] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch280);
            if (dst[279] != v) { dst[279] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch281);
            if (dst[280] != v) { dst[280] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch282);
            if (dst[281] != v) { dst[281] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch283);
            if (dst[282] != v) { dst[282] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch284);
            if (dst[283] != v) { dst[283] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch285);
            if (dst[284] != v) { dst[284] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch286);
            if (dst[285] != v) { dst[285] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch287);
            if (dst[286] != v) { dst[286] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch288);
            if (dst[287] != v) { dst[287] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch289);
            if (dst[288] != v) { dst[288] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch290);
            if (dst[289] != v) { dst[289] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch291);
            if (dst[290] != v) { dst[290] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch292);
            if (dst[291] != v) { dst[291] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch293);
            if (dst[292] != v) { dst[292] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch294);
            if (dst[293] != v) { dst[293] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch295);
            if (dst[294] != v) { dst[294] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch296);
            if (dst[295] != v) { dst[295] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch297);
            if (dst[296] != v) { dst[296] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch298);
            if (dst[297] != v) { dst[297] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch299);
            if (dst[298] != v) { dst[298] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch300);
            if (dst[299] != v) { dst[299] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch301);
            if (dst[300] != v) { dst[300] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch302);
            if (dst[301] != v) { dst[301] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch303);
            if (dst[302] != v) { dst[302] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch304);
            if (dst[303] != v) { dst[303] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch305);
            if (dst[304] != v) { dst[304] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch306);
            if (dst[305] != v) { dst[305] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch307);
            if (dst[306] != v) { dst[306] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch308);
            if (dst[307] != v) { dst[307] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch309);
            if (dst[308] != v) { dst[308] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch310);
            if (dst[309] != v) { dst[309] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch311);
            if (dst[310] != v) { dst[310] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch312);
            if (dst[311] != v) { dst[311] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch313);
            if (dst[312] != v) { dst[312] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch314);
            if (dst[313] != v) { dst[313] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch315);
            if (dst[314] != v) { dst[314] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch316);
            if (dst[315] != v) { dst[315] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch317);
            if (dst[316] != v) { dst[316] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch318);
            if (dst[317] != v) { dst[317] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch319);
            if (dst[318] != v) { dst[318] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch320);
            if (dst[319] != v) { dst[319] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch321);
            if (dst[320] != v) { dst[320] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch322);
            if (dst[321] != v) { dst[321] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch323);
            if (dst[322] != v) { dst[322] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch324);
            if (dst[323] != v) { dst[323] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch325);
            if (dst[324] != v) { dst[324] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch326);
            if (dst[325] != v) { dst[325] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch327);
            if (dst[326] != v) { dst[326] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch328);
            if (dst[327] != v) { dst[327] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch329);
            if (dst[328] != v) { dst[328] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch330);
            if (dst[329] != v) { dst[329] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch331);
            if (dst[330] != v) { dst[330] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch332);
            if (dst[331] != v) { dst[331] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch333);
            if (dst[332] != v) { dst[332] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch334);
            if (dst[333] != v) { dst[333] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch335);
            if (dst[334] != v) { dst[334] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch336);
            if (dst[335] != v) { dst[335] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch337);
            if (dst[336] != v) { dst[336] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch338);
            if (dst[337] != v) { dst[337] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch339);
            if (dst[338] != v) { dst[338] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch340);
            if (dst[339] != v) { dst[339] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch341);
            if (dst[340] != v) { dst[340] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch342);
            if (dst[341] != v) { dst[341] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch343);
            if (dst[342] != v) { dst[342] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch344);
            if (dst[343] != v) { dst[343] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch345);
            if (dst[344] != v) { dst[344] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch346);
            if (dst[345] != v) { dst[345] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch347);
            if (dst[346] != v) { dst[346] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch348);
            if (dst[347] != v) { dst[347] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch349);
            if (dst[348] != v) { dst[348] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch350);
            if (dst[349] != v) { dst[349] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch351);
            if (dst[350] != v) { dst[350] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch352);
            if (dst[351] != v) { dst[351] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch353);
            if (dst[352] != v) { dst[352] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch354);
            if (dst[353] != v) { dst[353] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch355);
            if (dst[354] != v) { dst[354] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch356);
            if (dst[355] != v) { dst[355] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch357);
            if (dst[356] != v) { dst[356] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch358);
            if (dst[357] != v) { dst[357] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch359);
            if (dst[358] != v) { dst[358] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch360);
            if (dst[359] != v) { dst[359] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch361);
            if (dst[360] != v) { dst[360] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch362);
            if (dst[361] != v) { dst[361] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch363);
            if (dst[362] != v) { dst[362] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch364);
            if (dst[363] != v) { dst[363] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch365);
            if (dst[364] != v) { dst[364] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch366);
            if (dst[365] != v) { dst[365] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch367);
            if (dst[366] != v) { dst[366] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch368);
            if (dst[367] != v) { dst[367] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch369);
            if (dst[368] != v) { dst[368] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch370);
            if (dst[369] != v) { dst[369] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch371);
            if (dst[370] != v) { dst[370] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch372);
            if (dst[371] != v) { dst[371] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch373);
            if (dst[372] != v) { dst[372] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch374);
            if (dst[373] != v) { dst[373] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch375);
            if (dst[374] != v) { dst[374] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch376);
            if (dst[375] != v) { dst[375] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch377);
            if (dst[376] != v) { dst[376] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch378);
            if (dst[377] != v) { dst[377] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch379);
            if (dst[378] != v) { dst[378] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch380);
            if (dst[379] != v) { dst[379] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch381);
            if (dst[380] != v) { dst[380] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch382);
            if (dst[381] != v) { dst[381] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch383);
            if (dst[382] != v) { dst[382] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch384);
            if (dst[383] != v) { dst[383] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch385);
            if (dst[384] != v) { dst[384] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch386);
            if (dst[385] != v) { dst[385] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch387);
            if (dst[386] != v) { dst[386] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch388);
            if (dst[387] != v) { dst[387] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch389);
            if (dst[388] != v) { dst[388] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch390);
            if (dst[389] != v) { dst[389] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch391);
            if (dst[390] != v) { dst[390] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch392);
            if (dst[391] != v) { dst[391] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch393);
            if (dst[392] != v) { dst[392] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch394);
            if (dst[393] != v) { dst[393] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch395);
            if (dst[394] != v) { dst[394] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch396);
            if (dst[395] != v) { dst[395] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch397);
            if (dst[396] != v) { dst[396] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch398);
            if (dst[397] != v) { dst[397] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch399);
            if (dst[398] != v) { dst[398] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch400);
            if (dst[399] != v) { dst[399] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch401);
            if (dst[400] != v) { dst[400] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch402);
            if (dst[401] != v) { dst[401] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch403);
            if (dst[402] != v) { dst[402] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch404);
            if (dst[403] != v) { dst[403] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch405);
            if (dst[404] != v) { dst[404] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch406);
            if (dst[405] != v) { dst[405] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch407);
            if (dst[406] != v) { dst[406] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch408);
            if (dst[407] != v) { dst[407] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch409);
            if (dst[408] != v) { dst[408] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch410);
            if (dst[409] != v) { dst[409] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch411);
            if (dst[410] != v) { dst[410] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch412);
            if (dst[411] != v) { dst[411] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch413);
            if (dst[412] != v) { dst[412] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch414);
            if (dst[413] != v) { dst[413] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch415);
            if (dst[414] != v) { dst[414] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch416);
            if (dst[415] != v) { dst[415] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch417);
            if (dst[416] != v) { dst[416] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch418);
            if (dst[417] != v) { dst[417] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch419);
            if (dst[418] != v) { dst[418] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch420);
            if (dst[419] != v) { dst[419] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch421);
            if (dst[420] != v) { dst[420] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch422);
            if (dst[421] != v) { dst[421] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch423);
            if (dst[422] != v) { dst[422] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch424);
            if (dst[423] != v) { dst[423] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch425);
            if (dst[424] != v) { dst[424] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch426);
            if (dst[425] != v) { dst[425] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch427);
            if (dst[426] != v) { dst[426] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch428);
            if (dst[427] != v) { dst[427] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch429);
            if (dst[428] != v) { dst[428] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch430);
            if (dst[429] != v) { dst[429] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch431);
            if (dst[430] != v) { dst[430] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch432);
            if (dst[431] != v) { dst[431] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch433);
            if (dst[432] != v) { dst[432] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch434);
            if (dst[433] != v) { dst[433] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch435);
            if (dst[434] != v) { dst[434] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch436);
            if (dst[435] != v) { dst[435] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch437);
            if (dst[436] != v) { dst[436] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch438);
            if (dst[437] != v) { dst[437] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch439);
            if (dst[438] != v) { dst[438] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch440);
            if (dst[439] != v) { dst[439] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch441);
            if (dst[440] != v) { dst[440] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch442);
            if (dst[441] != v) { dst[441] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch443);
            if (dst[442] != v) { dst[442] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch444);
            if (dst[443] != v) { dst[443] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch445);
            if (dst[444] != v) { dst[444] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch446);
            if (dst[445] != v) { dst[445] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch447);
            if (dst[446] != v) { dst[446] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch448);
            if (dst[447] != v) { dst[447] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch449);
            if (dst[448] != v) { dst[448] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch450);
            if (dst[449] != v) { dst[449] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch451);
            if (dst[450] != v) { dst[450] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch452);
            if (dst[451] != v) { dst[451] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch453);
            if (dst[452] != v) { dst[452] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch454);
            if (dst[453] != v) { dst[453] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch455);
            if (dst[454] != v) { dst[454] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch456);
            if (dst[455] != v) { dst[455] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch457);
            if (dst[456] != v) { dst[456] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch458);
            if (dst[457] != v) { dst[457] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch459);
            if (dst[458] != v) { dst[458] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch460);
            if (dst[459] != v) { dst[459] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch461);
            if (dst[460] != v) { dst[460] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch462);
            if (dst[461] != v) { dst[461] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch463);
            if (dst[462] != v) { dst[462] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch464);
            if (dst[463] != v) { dst[463] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch465);
            if (dst[464] != v) { dst[464] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch466);
            if (dst[465] != v) { dst[465] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch467);
            if (dst[466] != v) { dst[466] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch468);
            if (dst[467] != v) { dst[467] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch469);
            if (dst[468] != v) { dst[468] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch470);
            if (dst[469] != v) { dst[469] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch471);
            if (dst[470] != v) { dst[470] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch472);
            if (dst[471] != v) { dst[471] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch473);
            if (dst[472] != v) { dst[472] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch474);
            if (dst[473] != v) { dst[473] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch475);
            if (dst[474] != v) { dst[474] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch476);
            if (dst[475] != v) { dst[475] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch477);
            if (dst[476] != v) { dst[476] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch478);
            if (dst[477] != v) { dst[477] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch479);
            if (dst[478] != v) { dst[478] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch480);
            if (dst[479] != v) { dst[479] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch481);
            if (dst[480] != v) { dst[480] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch482);
            if (dst[481] != v) { dst[481] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch483);
            if (dst[482] != v) { dst[482] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch484);
            if (dst[483] != v) { dst[483] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch485);
            if (dst[484] != v) { dst[484] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch486);
            if (dst[485] != v) { dst[485] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch487);
            if (dst[486] != v) { dst[486] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch488);
            if (dst[487] != v) { dst[487] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch489);
            if (dst[488] != v) { dst[488] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch490);
            if (dst[489] != v) { dst[489] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch491);
            if (dst[490] != v) { dst[490] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch492);
            if (dst[491] != v) { dst[491] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch493);
            if (dst[492] != v) { dst[492] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch494);
            if (dst[493] != v) { dst[493] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch495);
            if (dst[494] != v) { dst[494] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch496);
            if (dst[495] != v) { dst[495] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch497);
            if (dst[496] != v) { dst[496] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch498);
            if (dst[497] != v) { dst[497] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch499);
            if (dst[498] != v) { dst[498] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch500);
            if (dst[499] != v) { dst[499] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch501);
            if (dst[500] != v) { dst[500] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch502);
            if (dst[501] != v) { dst[501] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch503);
            if (dst[502] != v) { dst[502] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch504);
            if (dst[503] != v) { dst[503] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch505);
            if (dst[504] != v) { dst[504] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch506);
            if (dst[505] != v) { dst[505] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch507);
            if (dst[506] != v) { dst[506] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch508);
            if (dst[507] != v) { dst[507] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch509);
            if (dst[508] != v) { dst[508] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch510);
            if (dst[509] != v) { dst[509] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch511);
            if (dst[510] != v) { dst[510] = (byte)v; changed = true; }
            v = DmxValueUtils.ClampByte(Ch512);
            if (dst[511] != v) { dst[511] = (byte)v; changed = true; }
            return changed;
        }
    }
}
