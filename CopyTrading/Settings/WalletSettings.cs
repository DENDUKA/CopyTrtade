using CopyTrading.Models.Values;

namespace CopyTrading.Settings;

public static class WalletSettings
{
    public static Wallet[] TrackedWallets =
    [
        ///Хороший :
        new Wallet("0x6049ddfd35e4d9098a6367e36023f2a36792b642"),


        //Рассматриваю
        new Wallet("0xee62db4851c3c6c5bf8010e839f2ae15fb4764bb"),
        new Wallet("0x987163b6b482c30c2f5f3aa2760109668eb0091d"),
        new Wallet("0xf770f371cc66499a89ae56aa84f9506e083f99ea"),
        new Wallet("0x6e4d47dad1e97833f4ecb0ef56347ba8e6fd1c0e"),
        new Wallet("0x491251909171d4b9b39c5bc5d70515292e21eaf0"),

        new Wallet("0xba939edf38c0ae0cc689c98b492e0535f43e4550"),
        new Wallet("0x5a8e21c3a73cc4742ecf1f422379a6fbf1992a42"),

        new Wallet("0x1e37a337ed460039d1b15bd3bc489de789768d5e"),
        new Wallet("0xc2d4ff17906940004b25884779c74aecda24fb83"),
        new Wallet("0xdb6995164092fc54a0c33fdf032450d1f6f558a6"),
        new Wallet("0xa844d7ac9fa3424c4fd38a25baa23e460ec3e802"),
        new Wallet("0xff9152cce6fbd30988b1aef70df6f086c99e2c55"),
        new Wallet("0xbb088e9852e18b1df8890d8d0f48bdcec0ab0964"),
        new Wallet("0xf709deb9ca069e53a31a408fde397a87d025a352"),
        new Wallet("0xb48cd87a34e4a756f03bc3d0f78ef470937ec9a6"),
        new Wallet("0x685feceec46dd4e5c9b5b726f5d7550fd0eda526"),
        new Wallet("0x33c3b8bcd758f511b350374a63e1f880f40e0282"),



    ];

    //https://trysuper.co/copytrade

    public static string[] TestFillTrackedWallets =
    [
        "0x744cf47e88d9d0847544f0ac2fa7575cf5925f79"
    ];

    public static Wallet MyWallet = new("0x6e4d47dad1e97833f4ecb0ef56347ba8e6fd1c0e");
}
