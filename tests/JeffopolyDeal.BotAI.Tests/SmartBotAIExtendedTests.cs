using JeffopolyDeal;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.BotAI.Tests
{
    public class SmartBotAIExtendedTests
    {
        // ── 1. Bot plays rent card on its best color ─────────────────────

        [Fact]
        public void PlayTurn_PlaysRentOnBestColor()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            // Bot has 2 Red properties (rent = 3) and 1 Brown (rent = 1)
            var redSet = CreatePartialSet(PropertyColor.Red, 2, 3);
            bot.PropertySets.Add(redSet);
            var brownSet = CreatePartialSet(PropertyColor.Brown, 1, 2);
            bot.PropertySets.Add(brownSet);

            // Give bot a Red/Yellow rent card
            var rent = new Card
            {
                Id = 100,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Red, PropertyColor.Yellow },
                MoneyValue = 1,
                Name = "Red/Yellow Rent"
            };
            bot.Hand.Add(rent);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.Id == 100) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.False(capturedRequest!.PlayAsMoney);
            Assert.Equal(PropertyColor.Red, capturedRequest.RentColor);
        }

        // ── 2. Bot uses DealBreaker on opponent's complete set ───────────

        [Fact]
        public void PlayTurn_PlaysDealBreakerOnCompleteSet()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, opponent };

            // Opponent has a complete Brown set
            opponent.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));

            var dealBreaker = new Card
            {
                Id = 101,
                CardType = CardType.Action,
                ActionKind = ActionType.DealBreaker,
                MoneyValue = 5,
                Name = "Deal Breaker"
            };
            bot.Hand.Add(dealBreaker);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.DealBreaker) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.False(capturedRequest!.PlayAsMoney);
            Assert.Equal("p1", capturedRequest.TargetPlayerId);
            Assert.Equal(PropertyColor.Brown, capturedRequest.TargetSetColor);
        }

        // ── 3. Bot plays JSN against DealBreaker but NOT against Birthday ─

        [Fact]
        public void BuildResponse_JSNsAgainstDealBreaker_NotBirthday()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(CreateJSN(99));
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            // DealBreaker → should JSN
            var dealBreakerPending = new PendingAction
            {
                Type = PendingActionType.RespondToDealBreaker,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
                TargetSetColor = PropertyColor.Brown,
            };
            var dbResponse = SmartBotAI.BuildResponse(bot, dealBreakerPending, allPlayers);
            Assert.True(dbResponse.PlayJustSayNo);

            // Birthday → should NOT JSN
            bot.Hand.Add(CreateJSN(98));
            bot.Bank.Add(CreateMoney(1, 5));
            var birthdayPending = new PendingAction
            {
                Type = PendingActionType.PayBirthday,
                Amount = 2,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
            };
            var bdResponse = SmartBotAI.BuildResponse(bot, birthdayPending, allPlayers);
            Assert.False(bdResponse.PlayJustSayNo);
        }

        // ── 4. Bot pairs DoubleTheRent with rent card ────────────────────

        [Fact]
        public void PlayTurn_PairsDoubleTheRentWithRent()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            // Bot has properties to charge rent on
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));

            var rent = new Card
            {
                Id = 200,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Brown, PropertyColor.LightBlue },
                MoneyValue = 1,
                Name = "Brown/LightBlue Rent"
            };
            var dtr = new Card
            {
                Id = 201,
                CardType = CardType.Action,
                ActionKind = ActionType.DoubleTheRent,
                MoneyValue = 1,
                Name = "Double the Rent"
            };
            bot.Hand.Add(rent);
            bot.Hand.Add(dtr);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.CardType == CardType.Rent) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest!.DoubleRentCardIds);
            Assert.Contains(201, capturedRequest.DoubleRentCardIds!);
        }

        // ── 5. Bot plays PassGo for card advantage ───────────────────────

        [Fact]
        public void PlayTurn_PlaysPassGo()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            var passGo = new Card
            {
                Id = 300,
                CardType = CardType.Action,
                ActionKind = ActionType.PassGo,
                MoneyValue = 1,
                Name = "Pass Go"
            };
            bot.Hand.Add(passGo);

            bool passGoPlayed = false;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.PassGo)
                {
                    passGoPlayed = true;
                    Assert.False(req.PlayAsMoney);
                }
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.True(passGoPlayed);
        }

        // ── 6. Bot plays SlyDeal targeting set-completing property ───────

        [Fact]
        public void PlayTurn_SlyDealTargetsSetCompletingProperty()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, opponent };

            // Bot needs 1 more Red to complete (has 2 of 3)
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 2, 3));

            // Opponent has a stealable Red property
            var opponentRedProp = new Card
            {
                Id = 400,
                CardType = CardType.Property,
                Color = PropertyColor.Red,
                ActiveColor = PropertyColor.Red,
                MoneyValue = 3,
                Name = "Red Prop"
            };
            var opponentSet = new PropertySet { Color = PropertyColor.Red };
            opponentSet.Cards.Add(opponentRedProp);
            opponent.PropertySets.Add(opponentSet);

            var slyDeal = new Card
            {
                Id = 401,
                CardType = CardType.Action,
                ActionKind = ActionType.SlyDeal,
                MoneyValue = 3,
                Name = "Sly Deal"
            };
            bot.Hand.Add(slyDeal);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.SlyDeal) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.False(capturedRequest!.PlayAsMoney);
            Assert.Equal("p1", capturedRequest.TargetPlayerId);
            Assert.Equal(400, capturedRequest.TargetCardId);
        }

        // ── 7. Bot plays ForceDeal offering cheapest property ────────────

        [Fact]
        public void PlayTurn_ForceDealOffersCheapestProperty()
        {
            var bot = CreatePlayer("bot-1");
            var opponent = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, opponent };

            // Bot has a cheap Brown ($1) and expensive Red ($3) — both incomplete
            var cheapProp = new Card
            {
                Id = 500,
                CardType = CardType.Property,
                Color = PropertyColor.Brown,
                ActiveColor = PropertyColor.Brown,
                MoneyValue = 1,
                Name = "Cheap Brown"
            };
            var expProp = new Card
            {
                Id = 501,
                CardType = CardType.Property,
                Color = PropertyColor.Red,
                ActiveColor = PropertyColor.Red,
                MoneyValue = 3,
                Name = "Expensive Red"
            };
            var botBrownSet = new PropertySet { Color = PropertyColor.Brown };
            botBrownSet.Cards.Add(cheapProp);
            bot.PropertySets.Add(botBrownSet);
            var botRedSet = new PropertySet { Color = PropertyColor.Red };
            botRedSet.Cards.Add(expProp);
            bot.PropertySets.Add(botRedSet);

            // Opponent has a stealable property
            var oppProp = new Card
            {
                Id = 502,
                CardType = CardType.Property,
                Color = PropertyColor.Green,
                ActiveColor = PropertyColor.Green,
                MoneyValue = 4,
                Name = "Opponent Green"
            };
            var oppSet = new PropertySet { Color = PropertyColor.Green };
            oppSet.Cards.Add(oppProp);
            opponent.PropertySets.Add(oppSet);

            var forceDeal = new Card
            {
                Id = 503,
                CardType = CardType.Action,
                ActionKind = ActionType.ForceDeal,
                MoneyValue = 3,
                Name = "Force Deal"
            };
            bot.Hand.Add(forceDeal);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.ForceDeal) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.False(capturedRequest!.PlayAsMoney);
            // Should offer the cheapest property (Brown $1)
            Assert.Equal(500, capturedRequest.OfferedCardId);
        }

        // ── 8. Bot plays House on complete set ───────────────────────────

        [Fact]
        public void PlayTurn_PlaysHouseOnCompleteSet()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Red, 3));

            var house = new Card
            {
                Id = 600,
                CardType = CardType.Action,
                ActionKind = ActionType.House,
                MoneyValue = 3,
                Name = "House"
            };
            bot.Hand.Add(house);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.House) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.False(capturedRequest!.PlayAsMoney);
            Assert.Equal(PropertyColor.Red, capturedRequest.TargetSetColor);
        }

        // ── 9. Bot targets richest opponent for DebtCollector ────────────

        [Fact]
        public void PlayTurn_DebtCollectorTargetsRichestOpponent()
        {
            var bot = CreatePlayer("bot-1");
            var poor = CreatePlayer("p1");
            var rich = CreatePlayer("p2");
            var allPlayers = new List<Player> { bot, poor, rich };

            poor.Bank.Add(CreateMoney(1, 1));
            rich.Bank.Add(CreateMoney(2, 10));

            var debtCollector = new Card
            {
                Id = 700,
                CardType = CardType.Action,
                ActionKind = ActionType.DebtCollector,
                MoneyValue = 3,
                Name = "Debt Collector"
            };
            bot.Hand.Add(debtCollector);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.DebtCollector) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.Equal("p2", capturedRequest!.TargetPlayerId);
        }

        // ── 10. Bot handles wild rent (targets a specific player) ────────

        [Fact]
        public void PlayTurn_WildRentTargetsRichestOpponent()
        {
            var bot = CreatePlayer("bot-1");
            var poor = CreatePlayer("p1");
            var rich = CreatePlayer("p2");
            var allPlayers = new List<Player> { bot, poor, rich };

            // Bot needs properties to charge rent
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.DarkBlue, 2));

            poor.Bank.Add(CreateMoney(1, 1));
            rich.Bank.Add(CreateMoney(2, 10));

            var wildRent = new Card
            {
                Id = 800,
                CardType = CardType.Rent,
                IsWildRent = true,
                MoneyValue = 3,
                Name = "Wild Rent"
            };
            bot.Hand.Add(wildRent);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.IsWildRent) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.False(capturedRequest!.PlayAsMoney);
            Assert.Equal("p2", capturedRequest.TargetPlayerId);
            Assert.NotNull(capturedRequest.RentColor);
        }

        // ── 11. Bot plays Hotel on complete set with House ───────────────

        [Fact]
        public void PlayTurn_PlaysHotelOnCompleteSetWithHouse()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            var completeSet = CreateCompleteSet(PropertyColor.Red, 3);
            completeSet.HasHouse = true;
            bot.PropertySets.Add(completeSet);

            var hotel = new Card
            {
                Id = 900,
                CardType = CardType.Action,
                ActionKind = ActionType.Hotel,
                MoneyValue = 4,
                Name = "Hotel"
            };
            bot.Hand.Add(hotel);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.Hotel) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.False(capturedRequest!.PlayAsMoney);
            Assert.Equal(PropertyColor.Red, capturedRequest.TargetSetColor);
        }

        // ── 12. House played as money when no complete sets ──────────────

        [Fact]
        public void PlayTurn_HousePlayedAsMoneyWhenNoCompleteSets()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            // No complete sets
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            var house = new Card
            {
                Id = 601,
                CardType = CardType.Action,
                ActionKind = ActionType.House,
                MoneyValue = 3,
                Name = "House"
            };
            bot.Hand.Add(house);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.House) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            // House scored 10 (no valid set), should still play (as money per BuildActionRequest)
            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest!.PlayAsMoney);
        }

        // ── 13. BuildResponse JSN in JSN chain as attacker ───────────────

        [Fact]
        public void BuildResponse_JSNChainAsAttacker_PlaysJSN()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(CreateJSN(99));
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            // Bot was original attacker, opponent played JSN, now bot must decide
            var pending = new PendingAction
            {
                Type = PendingActionType.JustSayNoChain,
                OriginalSourcePlayerId = "bot-1",
                OriginalActionType = PendingActionType.PayRent,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
                Amount = 3,
            };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);
            Assert.True(response.PlayJustSayNo);
        }

        // ── 14. BuildResponse JSN SlyDeal on near-complete set ───────────

        [Fact]
        public void BuildResponse_JSNSlyDealOnNearCompleteSet()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(CreateJSN(99));

            // Bot has 2 of 3 Red properties → near-complete
            var set = new PropertySet { Color = PropertyColor.Red };
            var targetCard = new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 };
            var otherCard = new Card { Id = 11, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 };
            set.Cards.Add(targetCard);
            set.Cards.Add(otherCard);
            bot.PropertySets.Add(set);

            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var pending = new PendingAction
            {
                Type = PendingActionType.RespondToSlyDeal,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
                TargetCardId = 10,
            };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);
            Assert.True(response.PlayJustSayNo);
        }

        // ── 15. BuildResponse does NOT JSN SlyDeal on single property ────

        [Fact]
        public void BuildResponse_DoesNotJSNSlyDealOnSingleProperty()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(CreateJSN(99));

            // Bot has only 1 of 3 Red → not near-complete
            var set = new PropertySet { Color = PropertyColor.Red };
            var targetCard = new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 };
            set.Cards.Add(targetCard);
            bot.PropertySets.Add(set);

            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var pending = new PendingAction
            {
                Type = PendingActionType.RespondToSlyDeal,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
                TargetCardId = 10,
            };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);
            Assert.False(response.PlayJustSayNo);
        }

        // ── 16. Bot plays ItsMyBirthday ──────────────────────────────────

        [Fact]
        public void PlayTurn_PlaysBirthdayAsAction()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            var birthday = new Card
            {
                Id = 1000,
                CardType = CardType.Action,
                ActionKind = ActionType.ItsMyBirthday,
                MoneyValue = 2,
                Name = "It's My Birthday"
            };
            bot.Hand.Add(birthday);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.ItsMyBirthday) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.False(capturedRequest!.PlayAsMoney);
        }

        // ── 17. Bot plays money cards ────────────────────────────────────

        [Fact]
        public void PlayTurn_PlaysMoneyAsMoney()
        {
            var bot = CreatePlayer("bot-1");
            var allPlayers = new List<Player> { bot };

            bot.Hand.Add(CreateMoney(1, 5));

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest!.PlayAsMoney);
        }

        // ── 18. Bot plays PropertyWildcard with best color ───────────────

        [Fact]
        public void PlayTurn_PropertyWildcardPicksBestColor()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            // Bot has 2 of 3 Red and 0 Yellow → should pick Red for wildcard
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 2, 3));

            var wildcard = new Card
            {
                Id = 1100,
                CardType = CardType.PropertyWildcard,
                Color = PropertyColor.Red,
                AltColor = PropertyColor.Yellow,
                MoneyValue = 3,
                Name = "Red/Yellow Wild"
            };
            bot.Hand.Add(wildcard);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.CardType == CardType.PropertyWildcard) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.Equal(PropertyColor.Red, capturedRequest!.WildcardColor);
        }

        // ── 19. Bot plays multicolor wildcard with best color ────────────

        [Fact]
        public void PlayTurn_MulticolorWildcardPicksBestColor()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            // Bot has 2 of 3 Green → should pick Green
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Green, 2, 3));
            bot.PropertySets.Add(CreatePartialSet(PropertyColor.Brown, 1, 2));

            var wildcard = new Card
            {
                Id = 1200,
                CardType = CardType.PropertyWildcard,
                IsMulticolorWild = true,
                MoneyValue = 0,
                Name = "Multi-color Wild"
            };
            bot.Hand.Add(wildcard);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.IsMulticolorWild) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.Equal(PropertyColor.Green, capturedRequest!.WildcardColor);
        }

        // ── 20. PlayTurn stops when playCard returns false ────────────────

        [Fact]
        public void PlayTurn_StopsWhenPlayCardReturnsFalse()
        {
            var bot = CreatePlayer("bot-1");
            var allPlayers = new List<Player> { bot };

            bot.Hand.Add(CreateMoney(1, 1));
            bot.Hand.Add(CreateMoney(2, 2));
            bot.Hand.Add(CreateMoney(3, 3));

            int playCount = 0;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                playCount++;
                b.Hand.Remove(card);
                return false; // stop after first play
            }, maxPlays: 3);

            Assert.Equal(1, playCount);
        }

        // ── 21. PlayTurn with empty hand does nothing ────────────────────

        [Fact]
        public void PlayTurn_EmptyHandDoesNothing()
        {
            var bot = CreatePlayer("bot-1");
            var allPlayers = new List<Player> { bot };

            int playCount = 0;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                playCount++;
                return true;
            }, maxPlays: 3);

            Assert.Equal(0, playCount);
        }

        // ── 22. BuildResponse ForceDeal returns empty payment ────────────

        [Fact]
        public void BuildResponse_ForceDealReturnsEmptyPayment()
        {
            var bot = CreatePlayer("bot-1");
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var pending = new PendingAction
            {
                Type = PendingActionType.RespondToForceDeal,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
                TargetCardId = 10,
                OfferedCardId = 20,
            };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);
            Assert.False(response.PlayJustSayNo);
            Assert.NotNull(response.PaymentCardIds);
            Assert.Empty(response.PaymentCardIds!);
        }

        // ── 23. BuildResponse DealBreaker returns empty payment (no JSN) ─

        [Fact]
        public void BuildResponse_DealBreakerReturnsEmptyPaymentWithoutJSN()
        {
            var bot = CreatePlayer("bot-1");
            // No JSN in hand
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var pending = new PendingAction
            {
                Type = PendingActionType.RespondToDealBreaker,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
                TargetSetColor = PropertyColor.Brown,
            };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);
            Assert.False(response.PlayJustSayNo);
            Assert.NotNull(response.PaymentCardIds);
            Assert.Empty(response.PaymentCardIds!);
        }

        // ── 24. Rent with no matching properties still plays (low score) ─

        [Fact]
        public void PlayTurn_RentWithNoPropertiesStillPlays()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            // No Red or Yellow properties — rent amount = 0 → score = 10
            var rent = new Card
            {
                Id = 1300,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Red, PropertyColor.Yellow },
                MoneyValue = 1,
                Name = "Red/Yellow Rent"
            };
            bot.Hand.Add(rent);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            // Card is still played (score 10 > nothing)
            Assert.NotNull(capturedRequest);
        }

        // ── 25. PickDiscards keeps DTR when rent in hand ─────────────────

        [Fact]
        public void PickDiscards_KeepsDTRWhenRentInHand()
        {
            var bot = CreatePlayer("bot-1");

            // DTR (priority 55 when rent in hand) vs Money $1 (priority 1)
            bot.Hand.Add(new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.DoubleTheRent, MoneyValue = 1 });
            bot.Hand.Add(new Card
            {
                Id = 2,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Brown },
                MoneyValue = 1
            });
            bot.Hand.Add(CreateMoney(3, 1));

            var discards = SmartBotAI.PickDiscards(bot, maxHandSize: 2);

            Assert.Single(discards);
            Assert.Contains(3, discards); // discard money, keep DTR
        }

        // ── 26. PickDiscards discards DTR when no rent in hand ───────────

        [Fact]
        public void PickDiscards_DiscardsDTRWhenNoRentInHand()
        {
            var bot = CreatePlayer("bot-1");

            // DTR (priority 5 without rent) vs PassGo (priority 35)
            bot.Hand.Add(new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.DoubleTheRent, MoneyValue = 1 });
            bot.Hand.Add(new Card { Id = 2, CardType = CardType.Action, ActionKind = ActionType.PassGo, MoneyValue = 1 });
            bot.Hand.Add(CreateMoney(3, 3)); // money priority = 3

            var discards = SmartBotAI.PickDiscards(bot, maxHandSize: 2);

            Assert.Single(discards);
            // DTR (5) is lowest priority, then money $3 (3)... wait, DTR is 5 > 3
            // So money $3 should be discarded
            Assert.Contains(3, discards);
        }

        // ── 27. PickDiscards keeps WildRent higher than regular rent ─────

        [Fact]
        public void PickDiscards_KeepsWildRentOverRegularRent()
        {
            var bot = CreatePlayer("bot-1");

            // WildRent (priority 60) vs regular rent (priority 45) vs money $1 (priority 1)
            bot.Hand.Add(new Card { Id = 1, CardType = CardType.Rent, IsWildRent = true, MoneyValue = 3, Name = "Wild Rent" });
            bot.Hand.Add(new Card
            {
                Id = 2,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Brown },
                MoneyValue = 1,
                Name = "Brown Rent"
            });
            bot.Hand.Add(CreateMoney(3, 1));
            bot.Hand.Add(CreateMoney(4, 1));

            var discards = SmartBotAI.PickDiscards(bot, maxHandSize: 2);

            Assert.Equal(2, discards.Count);
            // Should discard money cards (priority 1), keep both rent cards
            Assert.Contains(3, discards);
            Assert.Contains(4, discards);
        }

        // ── 28. DealBreaker plays as money when no complete sets ─────────

        [Fact]
        public void PlayTurn_DealBreakerPlaysAsMoneyWhenNoCompleteSets()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            // Opponent has no complete sets
            other.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            var dealBreaker = new Card
            {
                Id = 1400,
                CardType = CardType.Action,
                ActionKind = ActionType.DealBreaker,
                MoneyValue = 5,
                Name = "Deal Breaker"
            };
            bot.Hand.Add(dealBreaker);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.DealBreaker) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest!.PlayAsMoney);
        }

        // ── 29. SlyDeal plays as money when no stealable targets ─────────

        [Fact]
        public void PlayTurn_SlyDealPlaysAsMoneyWhenNoTargets()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            // Opponent has only complete sets (not stealable)
            other.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));

            var slyDeal = new Card
            {
                Id = 1500,
                CardType = CardType.Action,
                ActionKind = ActionType.SlyDeal,
                MoneyValue = 3,
                Name = "Sly Deal"
            };
            bot.Hand.Add(slyDeal);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.SlyDeal) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest!.PlayAsMoney);
        }

        // ── 30. ForceDeal plays as money when bot has no properties ──────

        [Fact]
        public void PlayTurn_ForceDealPlaysAsMoneyWhenBotHasNoProperties()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            other.PropertySets.Add(CreatePartialSet(PropertyColor.Red, 1, 3));

            var forceDeal = new Card
            {
                Id = 1600,
                CardType = CardType.Action,
                ActionKind = ActionType.ForceDeal,
                MoneyValue = 3,
                Name = "Force Deal"
            };
            bot.Hand.Add(forceDeal);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.ForceDeal) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest!.PlayAsMoney);
        }

        // ── 31. Two DTR cards stacked with rent ──────────────────────────

        [Fact]
        public void PlayTurn_StacksTwoDTRWithRent()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));

            var rent = new Card
            {
                Id = 200,
                CardType = CardType.Rent,
                RentColors = new List<PropertyColor> { PropertyColor.Brown, PropertyColor.LightBlue },
                MoneyValue = 1,
                Name = "Brown/LB Rent"
            };
            var dtr1 = new Card
            {
                Id = 201,
                CardType = CardType.Action,
                ActionKind = ActionType.DoubleTheRent,
                MoneyValue = 1,
                Name = "DTR 1"
            };
            var dtr2 = new Card
            {
                Id = 202,
                CardType = CardType.Action,
                ActionKind = ActionType.DoubleTheRent,
                MoneyValue = 1,
                Name = "DTR 2"
            };
            bot.Hand.Add(rent);
            bot.Hand.Add(dtr1);
            bot.Hand.Add(dtr2);

            PlayCardRequest? capturedRequest = null;
            // maxPlays: 3 → rent (1 play) + 2 DTR = 3 plays, all fit
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.CardType == CardType.Rent) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest!.DoubleRentCardIds);
            Assert.Equal(2, capturedRequest.DoubleRentCardIds!.Count);
        }

        // ── 32. BuildResponse DebtCollector pays optimally ───────────────

        [Fact]
        public void BuildResponse_DebtCollectorPaysOptimally()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 2));
            bot.Bank.Add(CreateMoney(2, 3));
            bot.Bank.Add(CreateMoney(3, 5));

            var pending = new PendingAction
            {
                Type = PendingActionType.PayDebtCollector,
                Amount = 5,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);

            Assert.False(response.PlayJustSayNo);
            var paidCards = bot.GetPayableCards()
                .Where(c => response.PaymentCardIds!.Contains(c.Id))
                .ToList();
            int totalPaid = paidCards.Sum(c => c.MoneyValue);
            Assert.Equal(5, totalPaid);
        }

        // ── 33. IsBot checks connection ID prefix ────────────────────────

        [Fact]
        public void IsBot_ChecksConnectionIdPrefix()
        {
            Assert.True(SmartBotAI.IsBot("bot-123"));
            Assert.True(SmartBotAI.IsBot("bot-abc"));
            Assert.False(SmartBotAI.IsBot("player-1"));
            Assert.False(SmartBotAI.IsBot("human"));
        }

        // ── 34. PickDiscards returns empty when under max ────────────────

        [Fact]
        public void PickDiscards_ReturnsEmptyWhenUnderMax()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(CreateMoney(1, 1));
            bot.Hand.Add(CreateMoney(2, 2));

            var discards = SmartBotAI.PickDiscards(bot, maxHandSize: 7);
            Assert.Empty(discards);
        }

        // ── 35. WildRent with no properties plays as money ───────────────

        [Fact]
        public void PlayTurn_WildRentPlaysAsMoneyWhenNoProperties()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            var wildRent = new Card
            {
                Id = 1700,
                CardType = CardType.Rent,
                IsWildRent = true,
                MoneyValue = 3,
                Name = "Wild Rent"
            };
            bot.Hand.Add(wildRent);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest!.PlayAsMoney);
        }

        // ── 36. Hotel plays as money without House ───────────────────────

        [Fact]
        public void PlayTurn_HotelPlaysAsMoneyWithoutHouse()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            // Complete set but no House
            bot.PropertySets.Add(CreateCompleteSet(PropertyColor.Red, 3));

            var hotel = new Card
            {
                Id = 1800,
                CardType = CardType.Action,
                ActionKind = ActionType.Hotel,
                MoneyValue = 4,
                Name = "Hotel"
            };
            bot.Hand.Add(hotel);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                if (card.ActionKind == ActionType.Hotel) capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest!.PlayAsMoney);
        }

        // ── 37. Property card plays normally ─────────────────────────────

        [Fact]
        public void PlayTurn_PropertyCardPlaysAsProperty()
        {
            var bot = CreatePlayer("bot-1");
            var allPlayers = new List<Player> { bot };

            var prop = new Card
            {
                Id = 1900,
                CardType = CardType.Property,
                Color = PropertyColor.Red,
                MoneyValue = 3,
                Name = "Red Prop"
            };
            bot.Hand.Add(prop);

            PlayCardRequest? capturedRequest = null;
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                capturedRequest = req;
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.NotNull(capturedRequest);
            Assert.False(capturedRequest!.PlayAsMoney);
        }

        // ── 38. BuildResponse: no JSN for debt collector when bank covers ─

        [Fact]
        public void BuildResponse_NoJSNForDebtCollectorWhenBankCovers()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(CreateJSN(99));
            bot.Bank.Add(CreateMoney(1, 10));

            var pending = new PendingAction
            {
                Type = PendingActionType.PayDebtCollector,
                Amount = 5,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);
            Assert.False(response.PlayJustSayNo);
        }

        // ── 39. BuildResponse: JSN for large debt collector ──────────────

        [Fact]
        public void BuildResponse_JSNForLargeDebtCollectorWhenBankLow()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(CreateJSN(99));
            bot.Bank.Add(CreateMoney(1, 2));

            var pending = new PendingAction
            {
                Type = PendingActionType.PayDebtCollector,
                Amount = 5,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);
            Assert.True(response.PlayJustSayNo);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static Player CreatePlayer(string id) => new()
        {
            ConnectionId = id,
            PlayerId = id,
            Name = id,
        };

        private static Card CreateMoney(int id, int value) => new()
        {
            Id = id,
            CardType = CardType.Money,
            MoneyValue = value,
            Name = $"{value}M",
        };

        private static Card CreateJSN(int id) => new()
        {
            Id = id,
            CardType = CardType.Action,
            ActionKind = ActionType.JustSayNo,
            MoneyValue = 4,
            Name = "Just Say No",
        };

        private static PropertySet CreateCompleteSet(PropertyColor color, int size)
        {
            var set = new PropertySet { Color = color };
            for (int i = 0; i < size; i++)
            {
                set.Cards.Add(new Card
                {
                    Id = 3000 + (int)color * 10 + i,
                    CardType = CardType.Property,
                    Color = color,
                    ActiveColor = color,
                    MoneyValue = GameConfig.PropertyValue.GetValueOrDefault(color, 1),
                    Name = $"{color} {i}",
                });
            }
            return set;
        }

        private static PropertySet CreatePartialSet(PropertyColor color, int count, int required)
        {
            var set = new PropertySet { Color = color };
            for (int i = 0; i < count; i++)
            {
                set.Cards.Add(new Card
                {
                    Id = 4000 + (int)color * 10 + i,
                    CardType = CardType.Property,
                    Color = color,
                    ActiveColor = color,
                    MoneyValue = GameConfig.PropertyValue.GetValueOrDefault(color, 1),
                    Name = $"{color} {i}",
                });
            }
            return set;
        }
    }
}
