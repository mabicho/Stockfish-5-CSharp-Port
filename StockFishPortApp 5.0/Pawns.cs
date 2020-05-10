using System;
using System.Diagnostics;

using Key = System.UInt64;
using Bitboard = System.UInt64;
using Score = System.Int32;
using Square = System.Int32;
using Color = System.Int32;
using File = System.Int32;
using Rank = System.Int32;
using Value = System.Int32;
using V = System.Int32;

namespace StockFish
{
    public sealed class Pawns
    {
        public static Score S(int mg, int eg)
        {
            return Types.Make_score(mg, eg);
        }

        public sealed class Table : HashTable<Pawns.Entry>
        {
            public Table()
                : base(16384)
            {
                for (int i = 0; i < Size; i++)
                    this.table[i] = new Pawns.Entry();
            }
        }

        // Doubled pawn penalty by file
        public static Score[] Doubled = new Score[] {
            S(13, 43), S(20, 48), S(23, 48), S(23, 48),
            S(23, 48), S(23, 48), S(20, 48), S(13, 43) };

        // Isolated pawn penalty by opposed flag and file
        public static Score[][] Isolated = new Score[][] {
        new Score[] {   S(37, 45), S(54, 52), S(60, 52), S(60, 52),
                        S(60, 52), S(60, 52), S(54, 52), S(37, 45) },
        new Score[] {   S(25, 30), S(36, 35), S(40, 35), S(40, 35),
                        S(40, 35), S(40, 35), S(36, 35), S(25, 30) }};

        // Backward pawn penalty by opposed flag and file
        public static Score[][] Backward = new Score[][] {
        new Score[] {   S(30, 42), S(43, 46), S(49, 46), S(49, 46),
                        S(49, 46), S(49, 46), S(43, 46), S(30, 42) },
        new Score[] {   S(20, 28), S(29, 31), S(33, 31), S(33, 31),
                        S(33, 31), S(33, 31), S(29, 31), S(20, 28) }};

        public static int[] bonusesByFile = new int[]{ 1, 3, 3, 4, 4, 3, 3, 1 };

        // Connected pawn bonus by file and rank (initialized by formula)
        public static Score[/*FILE_NB*/][/*RANK_NB*/] Connected;

        // Candidate passed pawn bonus by rank
        //public static readonly Score[] CandidateBonus = new Score[] {
        public static Score[] CandidatePassed = new Score[] {
            S( 0, 0), S( 6, 13), S(6,13), S(14,29),
            S(34,68), S(83,166), S(0, 0), S( 0, 0)
        };

        // Bonus for file distance of the two outermost pawns
        public static Score PawnsFileSpan = S(0, 15);

        // Unsupported pawn penalty
        public static Score UnsupportedPawnPenalty = S(20, 10);

        // Weakness of our pawn shelter in front of the king indexed by [rank]
        public static Value[] ShelterWeakness = new Value[RankS.RANK_NB]
        {100, 0, 27, 73, 92, 101, 101, 0 };

        // Danger of enemy pawns moving toward our king indexed by
        // [no friendly pawn | pawn unblocked | pawn blocked][rank of enemy pawn]
        public static Value[][] StormDanger = new Value[][] {
            new Value[RankS.RANK_NB] { 0, 64, 128, 51, 26, 0, 0, 0 },
            new Value[RankS.RANK_NB] { 26,  32,  96, 38, 20, 0, 0, 0 },
            new Value[RankS.RANK_NB] {  0,   0, 160, 25, 13, 0, 0, 0 }};

        // Max bonus for king safety. Corresponds to start position with all the pawns
        // in front of the king and no enemy pawn on the horizon.
        const Value MaxSafetyBonus = (263);

        public static Bitboard MiddleEdges = (BitBoard.FileABB | BitBoard.FileHBB) & (BitBoard.Rank2BB | BitBoard.Rank3BB);

        /// <summary>
        /// Pawns::Entry contains various information about a pawn structure. A lookup
        /// to the pawn hash table (performed by calling the probe function) returns a
        /// pointer to an Entry object.
        /// </summary>
        public sealed class Entry
        {
            public Key key;
            public Score value;
            public Bitboard[] passedPawns = new Bitboard[ColorS.COLOR_NB];
            public Bitboard[] candidatePawns = new Bitboard[ColorS.COLOR_NB];
            public Bitboard[] pawnAttacks = new Bitboard[ColorS.COLOR_NB];
            public Square[] kingSquares = new Square[ColorS.COLOR_NB];
            public Score[] kingSafety = new Square[ColorS.COLOR_NB];
            public int[] minKPdistance = new int[ColorS.COLOR_NB];
            public int[] castlingRights = new int[ColorS.COLOR_NB];
            public int[] semiopenFiles = new int[ColorS.COLOR_NB];
            public int[][] pawnsOnSquares = new int[][] { new int[ColorS.COLOR_NB], new int[ColorS.COLOR_NB] }; // [color][light/dark squares]

            public Score Pawns_value() { return value; }
            public Bitboard Pawn_attacks(Color c) { return pawnAttacks[c]; }
            public Bitboard Passed_pawns(Color c) { return passedPawns[c]; }
            public Bitboard Candidate_pawns(Color c) { return candidatePawns[c]; }
            public int Semiopen_file(Color c, File f)
            {
                return semiopenFiles[c] & (1 << (int)(f));
            }

            public int Semiopen_side(Color c, File f, bool leftSide)
            {
                return semiopenFiles[c] & (leftSide ? ((1 << f) - 1) : ~((1 << (f + 1)) - 1));
            }

            public int Pawns_on_same_color_squares(Color c, Square s)
            {
                return pawnsOnSquares[c][(BitBoard.DarkSquares & BitBoard.SquareBB[s]) != 0 ? 1 : 0];
            }

            public Score King_safety(Position pos, Square ksq, Color Us)
            {
                return kingSquares[Us] == ksq && castlingRights[Us] == pos.can_castle_color(Us)
                    ? kingSafety[Us] : (kingSafety[Us] = Do_king_safety(pos, ksq, Us));
            }

            /// <summary>
            /// Entry::shelter_storm() calculates shelter and storm penalties for the file
            /// the king is on, as well as the two adjacent files.
            /// </summary>
            public Value Shelter_storm(Position pos, Square ksq, Color Us)
            {
                Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);

                Value safety = Pawns.MaxSafetyBonus;
                Bitboard b = pos.pieces_piecetype(PieceTypeS.PAWN) & (BitBoard.In_front_bb(Us, Types.Rank_of(ksq)) | BitBoard.Rank_bb_square(ksq));
                Bitboard ourPawns = b & pos.pieces_color(Us);
                Bitboard theirPawns = b & pos.pieces_color(Them);
                Rank rkUs, rkThem;
                File kf = Math.Max(FileS.FILE_B, Math.Min(FileS.FILE_G, Types.File_of(ksq)));

                for (File f = kf - 1; f <= kf + 1; ++f)
                {
                    b = ourPawns & BitBoard.File_bb_file(f);
                    rkUs = b != 0 ? Types.Relative_rank_square(Us, BitBoard.Backmost_sq(Us, b)) : RankS.RANK_1;

                    b = theirPawns & BitBoard.File_bb_file(f);
                    rkThem = b != 0 ? Types.Relative_rank_square(Us, BitBoard.Frontmost_sq(Them, b)) : RankS.RANK_1;

                    if ((MiddleEdges & BitBoard.SquareBB[Types.Make_square(f, rkThem)]) != 0
                        && Types.File_of(ksq) == f
                        && Types.Relative_rank_square(Us, ksq) == rkThem - 1)
                    {
                        safety += 200;
                    }
                    else
                    {
                        safety -= ShelterWeakness[rkUs]
                               + StormDanger[rkUs == RankS.RANK_1 ? 0 : rkThem == rkUs + 1 ? 2 : 1][rkThem];
                    }
                }

                return safety;
            }

            /// <summary>
            /// Entry::do_king_safety() calculates a bonus for king safety. It is called only
            /// when king square changes, which is about 20% of total king_safety() calls.
            /// </summary>
            public Score Do_king_safety(Position pos, Square ksq, Color Us)
            {
                kingSquares[Us] = ksq;
                castlingRights[Us] = pos.can_castle_color(Us);
                minKPdistance[Us] = 0;

                Bitboard pawns = pos.pieces_color_piecetype(Us, PieceTypeS.PAWN);
                if (pawns != 0)
                    while (0==(BitBoard.DistanceRingsBB[ksq][minKPdistance[Us]++] & pawns)) { }

                if (Types.Relative_rank_square(Us, ksq) > RankS.RANK_4)
                    return Types.Make_score(0, -16 * minKPdistance[Us]);

                Value bonus = Shelter_storm(pos, ksq, Us);

                // If we can castle use the bonus after the castle if is bigger
                if (pos.can_castle_castleright((new MakeCastlingS(Us, CastlingSideS.KING_SIDE)).right) != 0)
                    bonus = Math.Max(bonus, Shelter_storm(pos, Types.Relative_square(Us, SquareS.SQ_G1), Us));

                if (pos.can_castle_castleright((new MakeCastlingS(Us, CastlingSideS.QUEEN_SIDE)).right) != 0)
                    bonus = Math.Max(bonus, Shelter_storm(pos, Types.Relative_square(Us, SquareS.SQ_C1), Us));

                return Types.Make_score(bonus, -16 * minKPdistance[Us]);
            }
        }

        public static void Init()
        {
            int bonus;

            //Score[/*FILE_NB*/][/*RANK_NB*/] Connected
            Connected= new Score[FileS.FILE_NB][];
            for (File f = FileS.FILE_A; f <= FileS.FILE_H; ++f){
                Connected[f]= new Score[RankS.RANK_NB];
            }

            for (Rank r = RankS.RANK_1; r < RankS.RANK_8; ++r)
            {
                 for (File f = FileS.FILE_A; f <= FileS.FILE_H; ++f)
                {
                    bonus = (r * (r - 1) * (r - 2)) + (bonusesByFile[f] * ((r / 2) + 1));
                    Connected[f][r] = Types.Make_score(bonus, bonus);
                }
            }
        }
        public static Score Evaluate(Position pos, Pawns.Entry e, Color Us)
        {
            Color Them = (Us == ColorS.WHITE ? ColorS.BLACK : ColorS.WHITE);
            Square Up = (Us == ColorS.WHITE ? SquareS.DELTA_N : SquareS.DELTA_S);
            Square Right = (Us == ColorS.WHITE ? SquareS.DELTA_NE : SquareS.DELTA_SW);
            Square Left = (Us == ColorS.WHITE ? SquareS.DELTA_NW : SquareS.DELTA_SE);

            Bitboard b, p, doubled;
            Square s;
            File f;
            Rank r;
            bool passed, isolated, opposed, connected, backward, candidate, unsupported;
            Score value = ScoreS.SCORE_ZERO;
            Square[] pl = pos.list(Us, PieceTypeS.PAWN);
            int plPos = 0;

            Bitboard ourPawns = pos.pieces_color_piecetype(Us, PieceTypeS.PAWN);
            Bitboard theirPawns = pos.pieces_color_piecetype(Them, PieceTypeS.PAWN);

            e.passedPawns[Us] = e.candidatePawns[Us] = 0;
            e.kingSquares[Us] = SquareS.SQ_NONE;
            e.semiopenFiles[Us] = 0xFF;
            e.pawnAttacks[Us] = BitBoard.Shift_bb(ourPawns, Right) | BitBoard.Shift_bb(ourPawns, Left);
            e.pawnsOnSquares[Us][ColorS.BLACK] = Bitcount.Popcount_Max15(ourPawns & BitBoard.DarkSquares);
            e.pawnsOnSquares[Us][ColorS.WHITE] = pos.count(Us, PieceTypeS.PAWN) - e.pawnsOnSquares[Us][ColorS.BLACK];

            // Loop through all pawns of the current color and score each pawn
            while ((s = pl[plPos++]) != SquareS.SQ_NONE)
            {
                Debug.Assert(pos.piece_on(s) == Types.Make_piece(Us, PieceTypeS.PAWN));

                f = Types.File_of(s);

                // This file cannot be semi-open
                e.semiopenFiles[Us] &= ~(1 << f);

                // Previous rank
                p = BitBoard.Rank_bb_square(s - Types.Pawn_push(Us));

                // Our rank plus previous one
                b = BitBoard.Rank_bb_square(s) | p;

                // Flag the pawn as passed, isolated, doubled,
                // unsupported or connected (but not the backward one).
                connected = (ourPawns & BitBoard.Adjacent_files_bb(f) & b)!=0;
                unsupported = (0==(ourPawns & BitBoard.Adjacent_files_bb(f) & p));
                isolated = (0==(ourPawns & BitBoard.Adjacent_files_bb(f)));
                doubled = ourPawns & BitBoard.Forward_bb(Us, s);
                opposed = (theirPawns & BitBoard.Forward_bb(Us, s))!=0;
                passed = (0==(theirPawns & BitBoard.Passed_pawn_mask(Us, s)));

                // Test for backward pawn.
                // If the pawn is passed, isolated, or connected it cannot be
                // backward. If there are friendly pawns behind on adjacent files
                // or if it can capture an enemy pawn it cannot be backward either.
                if ((passed | isolated | connected)
                    || (ourPawns & BitBoard.Pawn_attack_span(Them, s)) != 0
                    || (pos.attacks_from_pawn(s, Us) & theirPawns) != 0)
                {
                    backward = false;
                }
                else
                {
                    // We now know that there are no friendly pawns beside or behind this
                    // pawn on adjacent files. We now check whether the pawn is
                    // backward by looking in the forward direction on the adjacent
                    // files, and picking the closest pawn there.
                    b = BitBoard.Pawn_attack_span(Us, s) & (ourPawns | theirPawns);
                    b = BitBoard.Pawn_attack_span(Us, s) & BitBoard.Rank_bb_square(BitBoard.Backmost_sq(Us, b));

                    // If we have an enemy pawn in the same or next rank, the pawn is
                    // backward because it cannot advance without being captured.
                    backward = ((b | BitBoard.Shift_bb(b, Up)) & theirPawns) != 0;
                }

                Debug.Assert(opposed | passed | (BitBoard.Pawn_attack_span(Us, s) & theirPawns)!=0);

                // A not-passed pawn is a candidate to become passed, if it is free to
                // advance and if the number of friendly pawns beside or behind this
                // pawn on adjacent files is higher than or equal to the number of
                // enemy pawns in the forward direction on the adjacent files.
                candidate = !(opposed | passed | backward | isolated)
                         && (b = BitBoard.Pawn_attack_span(Them, s + Types.Pawn_push(Us)) & ourPawns) != 0
                         && Bitcount.Popcount_Max15(b) >= Bitcount.Popcount_Max15(BitBoard.Pawn_attack_span(Us, s) & theirPawns);

                // Passed pawns will be properly scored in evaluation because we need
                // full attack info to evaluate passed pawns. Only the frontmost passed
                // pawn on each file is considered a true passed pawn.
                if (passed && 0==doubled)
                    e.passedPawns[Us] |= BitBoard.SquareBB[s];

                // Score this pawn
                if (isolated)
                    value -= Isolated[opposed ? 1 : 0][f];

                if (unsupported && !isolated)
                    value -= UnsupportedPawnPenalty;

                if (doubled!=0)
                    value -= Types.DivScore(Doubled[f], BitBoard.Rank_distance(s, BitBoard.Lsb(doubled)));

                if (backward)
                    value -= Backward[opposed ? 1 : 0][f];

                if (connected)
                    value += Connected[f][Types.Relative_rank_square(Us, s)];

                if (candidate)
                {
                    value += CandidatePassed[Types.Relative_rank_square(Us, s)];

                    if (0==doubled)
                        e.candidatePawns[Us] |= BitBoard.SquareBB[s];
                }
            }

            // In endgame it's better to have pawns on both wings. So give a bonus according
            // to file distance between left and right outermost pawns.
            if (pos.count(Us, PieceTypeS.PAWN) > 1)
            {
                b = (Bitboard)(e.semiopenFiles[Us] ^ 0xFF);
                value += PawnsFileSpan * (BitBoard.Msb(b) - BitBoard.Lsb(b));
            }

            return value;
        }

        /// <summary>
        /// probe() takes a position object as input, computes a Entry object, and returns
        /// a pointer to it. The result is also stored in a hash table, so we don't have
        /// to recompute everything when the same pawn structure occurs again.
        /// </summary>
        public static Pawns.Entry Probe(Position pos, Pawns.Table entries)
        {
            Key key = pos.pawn_key();
            Pawns.Entry e = entries[key];

            if (e.key == key)
                return e;

            e.key = key;
            e.value = Evaluate(pos, e, ColorS.WHITE) - Evaluate(pos, e, ColorS.BLACK);

            return e;
        }
    }
}
