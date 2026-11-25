namespace AMRSurveillance.Services;

public static class KenyaAdministrativeUnits
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>>
        CountySubCounties =
            new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.OrdinalIgnoreCase)
            {
                // 01. Mombasa
                ["Mombasa"] = ["Changamwe", "Jomvu", "Kisauni", "Likoni", "Mvita", "Nyali"],

                // 02. Kwale
                ["Kwale"] = ["Kinango", "Lunga Lunga", "Matuga", "Msambweni", "Samburu"],

                // 03. Kilifi
                ["Kilifi"] = ["Chonyi", "Ganze", "Kaloleni", "Kauma", "Kilifi North", "Kilifi South", "Magarini", "Malindi", "Rabai"],

                // 04. Tana River
                ["Tana River"] = ["Tana River", "Tana Delta", "Tana North", "Galedyertu", "Bangal"],

                // 05. Lamu
                ["Lamu"] = ["Lamu East", "Lamu West"],

                // 06. Taita-Taveta
                ["Taita-Taveta"] = ["Mwatate", "Taveta", "Voi", "Wundanyi"],

                // 07. Garissa
                ["Garissa"] = ["Balambala", "Dadaab", "Fafi", "Garissa", "Hulugho", "Ijara", "Lagdera"],

                // 08. Wajir
                ["Wajir"] = ["Buna", "Eldas", "Habaswein", "Tarbaj", "Wajir East", "Wajir North", "Wajir South", "Wajir West"],

                // 09. Mandera
                ["Mandera"] = ["Banissa", "Kutulo", "Lafey", "Mandera Central", "Mandera East", "Mandera North", "Mandera West"],

                // 10. Marsabit
                ["Marsabit"] = ["Laisamis", "Loiyangalani", "Marsabit Central", "Marsabit North", "Marsabit South", "Moyale", "North Horr", "Sololo"],

                // 11. Isiolo
                ["Isiolo"] = ["Garbatulla", "Isiolo", "Merti"],

                // 12. Meru
                ["Meru"] = ["Buuri East", "Buuri West", "Igembe Central", "Igembe North", "Igembe South", "Imenti North", "Imenti South", "Meru Central", "Tigania Central", "Tigania East", "Tigania West"],

                // 13. Tharaka-Nithi
                ["Tharaka-Nithi"] = ["Chuka", "Igambang'ombe", "Maara", "Tharaka North", "Tharaka South"],

                // 14. Embu
                ["Embu"] = ["Embu East", "Embu North", "Embu West", "Mbeere North", "Mbeere South"],

                // 15. Kitui
                ["Kitui"] = ["Ikutha", "Katulani", "Kisasi", "Kitui Central", "Kitui West", "Kyuso", "Lower Yatta", "Matinyani", "Migwani", "Mumoni", "Mutitu", "Mutitu North", "Mutomo", "Mwingi Central", "Mwingi East", "Nzambani", "Thagicu", "Tseikuru"],

                // 16. Machakos
                ["Machakos"] = ["Athi River (Mavoko)", "Kalama", "Kangundo", "Kathiani", "Machakos", "Masinga", "Matungulu", "Mwala", "Yatta"],

                // 17. Makueni
                ["Makueni"] = ["Kaiti", "Kibwezi East", "Kibwezi West", "Kilome", "Makueni", "Mbooni", "Mukaa"],

                // 18. Nyandarua
                ["Nyandarua"] = ["Kinangop", "Kipipiri", "Ndaragwa", "Ol Jorok", "Ol Kalou"],

                // 19. Nyeri
                ["Nyeri"] = ["Kieni East", "Kieni West", "Mathira East", "Mathira West", "Mukurwe-ini", "Nyeri Central", "Nyeri South", "Othaya", "Tetu"],

                // 20. Kirinyaga
                ["Kirinyaga"] = ["Kirinyaga Central", "Kirinyaga East", "Kirinyaga West", "Mwea East", "Mwea West"],

                // 21. Murang'a
                ["Murang'a"] = ["Gatanga", "Kahuro", "Kandara", "Kangema", "Kigumo", "Mathioya", "Murang'a East", "Murang'a South"],

                // 22. Kiambu
                ["Kiambu"] = ["Gatundu North", "Gatundu South", "Githunguri", "Juja", "Kabete", "Kiambu", "Kiambaa", "Kikuyu", "Limuru", "Lari", "Ruiru", "Thika East", "Thika West"],

                // 23. Turkana
                ["Turkana"] = ["Loima", "Lokichar", "Lokichoggio", "Turkana Central", "Turkana East", "Turkana North", "Turkana South", "Turkana West"],

                // 24. West Pokot
                ["West Pokot"] = ["Kipkomo", "Pokot Central", "Pokot North", "Pokot South", "West Pokot"],

                // 25. Samburu
                ["Samburu"] = ["Samburu Central", "Samburu East", "Samburu North"],

                // 26. Trans Nzoia
                ["Trans Nzoia"] = ["Cherang'any", "Endebess", "Kiminini", "Kwanza", "Saboti"],

                // 27. Uasin Gishu
                ["Uasin Gishu"] = ["Ainabkoi", "Kapsaret", "Kesses", "Moiben", "Soy", "Turbo"],

                // 28. Elgeyo-Marakwet
                ["Elgeyo-Marakwet"] = ["Keiyo North", "Keiyo South", "Marakwet East", "Marakwet West"],

                // 29. Nandi
                ["Nandi"] = ["Aldai", "Chesumei", "Emgwen", "Mosop", "Nandi East", "Nandi Hills", "Tindiret"],

                // 30. Baringo
                ["Baringo"] = ["Baringo Central", "Baringo North", "Baringo South", "Eldama Ravine", "Mogotio", "Tiaty"],

                // 31. Laikipia
                ["Laikipia"] = ["Laikipia Central", "Laikipia East", "Laikipia North", "Laikipia West", "Nanyuki"],

                // 32. Nakuru
                ["Nakuru"] = ["Bahati", "Gilgil", "Kuresoi North", "Kuresoi South", "Molo", "Naivasha", "Nakuru East", "Nakuru North", "Nakuru Town West", "Njoro", "Rongai", "Subukia"],

                // 33. Narok
                ["Narok"] = ["Emurua Dikirr", "Kilgoris", "Narok East", "Narok North", "Narok South", "Narok West", "Transmara East", "Transmara West"],

                // 34. Kajiado
                ["Kajiado"] = ["Isinya", "Kajiado Central", "Kajiado North", "Kajiado South", "Loitokitok", "Mashuuru"],

                // 35. Kericho
                ["Kericho"] = ["Ainamoi", "Belgut", "Bureti", "Kipkelion East", "Kipkelion West", "Soin Sigowet"],

                // 36. Bomet
                ["Bomet"] = ["Bomet Central", "Bomet East", "Chepalungu", "Konoin", "Sotik"],

                // 37. Kakamega
                ["Kakamega"] = ["Butere", "Lurambi", "Shinyalu", "Malava", "Ikolomani", "Khwisero", "Likuyani", "Lugari", "Matete", "Matungu", "Mumias East", "Mumias West", "Navakholo"],

                // 38. Vihiga
                ["Vihiga"] = ["Emuhaya", "Hamisi", "Luanda", "Sabatia", "Vihiga"],

                // 39. Bungoma
                ["Bungoma"] = ["Bumula", "Kabuchai", "Kanduyi", "Kimilili", "Mt. Elgon", "Sirisia", "Tongaren", "Webuye East", "Webuye West"],

                // 40. Busia
                ["Busia"] = ["Bunyala", "Butula", "Matayos", "Nambale", "Samia", "Teso North", "Teso South"],

                // 41. Siaya
                ["Siaya"] = ["Alego Usonga", "Bondo", "Gem", "Rarieda", "Ugenya", "Ugunja"],

                // 42. Kisumu
                ["Kisumu"] = ["Kisumu Central", "Kisumu East", "Kisumu West", "Muhoroni", "Nyakach", "Nyando", "Seme"],

                // 43. Homa Bay
                ["Homa Bay"] = ["Homa Bay Town", "Rachuonyo East", "Rachuonyo North", "Rachuonyo South", "Ndhiwa", "Rangwe", "Suba North", "Suba South"],

                // 44. Migori
                ["Migori"] = ["Awendo", "Kuria East", "Kuria West", "Migori", "Nyatike", "Rongo", "Suna East", "Suna West", "Uriri"],

                // 45. Kisii
                ["Kisii"] = ["Bobasi", "Bonchari", "Bomachoge Borabu", "Bomachoge Chache", "Kitutu Chache North", "Kitutu Chache South", "Nyaribari Chache", "Nyaribari Masaba", "South Mugirango"],

                // 46. Nyamira
                ["Nyamira"] = ["Borabu", "Manga", "Masaba North", "Nyamira North", "Nyamira South"],

                // 47. Nairobi City
                ["Nairobi"] = ["Dagoretti North", "Dagoretti South", "Embakasi Central", "Embakasi East", "Embakasi North", "Embakasi South", "Embakasi West", "Kamukunji", "Kasarani", "Kibra", "Lang'ata", "Makadara", "Mathare", "Roysambu", "Ruaraka", "Starehe", "Westlands"],

                // 48. Other areas outside Kenyan administrative boundaries
                ["Other"] = ["Other"],

                // 49. Not stated, or Unknown
                ["Unknown"] = ["Unknown"]
            };
}